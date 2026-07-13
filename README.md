# SmartWattWatt

A monorepo application suite for coordinating **Octopus Energy** EV charging schedules with **Fox ESS** inverter work mode control.

## Status

Implementation in progress. See [SmartWattWattFunc specification](docs/specs/SmartWattWattFunc.md).

## Components

| Component | Description | Status |
|---|---|---|
| `SmartWattWattFunc` | Azure Function — progressive Force Charge staging for Octopus EV dispatches | Implemented |

## Local development

To run locally you need **Azurite** (Azure Storage emulator). Install it once, then run `azurite --silent` in a **separate terminal window** and leave it running while the function host is up. Timer triggers use `UseDevelopmentStorage=true` in `local.settings.json`, which connects to Azurite on `127.0.0.1:10000`.

### Prerequisites

| Requirement | Notes |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | Project targets .NET 10 |
| [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local) | Required for `func start` and `dotnet run` |
| [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) | **Required.** Install globally, then run `azurite --silent` in a separate terminal before `func start` |

Install Azure Functions Core Tools (pick one):

```powershell
winget install Microsoft.Azure.FunctionsCoreTools
```

Or via npm:

```powershell
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

Restart your terminal, then confirm:

```powershell
func --version
```

Install **Azurite** (required for local timer triggers):

```powershell
npm install -g azurite
```

Confirm it is installed:

```powershell
azurite --version
```

Start Azurite in a **separate terminal window** and leave it running:

```powershell
azurite --silent
```

That window must stay open for the whole local session. Azurite listens on `127.0.0.1:10000` (blob), `:10001` (queue), and `:10002` (table).

Alternatively, install the [Azurite VS Code extension](https://marketplace.visualstudio.com/items?itemName=Azurite.azurite) and start it from the command palette.

### 1. Create your local settings (not committed to git)

Copy the example file and add your real values:

```powershell
Copy-Item src/SmartWattWattFunc/local.settings.example.json src/SmartWattWattFunc/local.settings.json
```

Edit `src/SmartWattWattFunc/local.settings.json` and set:

| Key | Description |
|---|---|
| `Octopus:AccountNumber` | Your Octopus account number |
| `Octopus:ApiKey` | Your Octopus personal API key |
| `FoxEss:DeviceSerialNumber` | Your inverter serial number (`sn`) |
| `FoxEss:ApiToken` | Your Fox ESS API token |

Leave `KeyVaultUri` empty for local runs — secrets are read directly from `local.settings.json`.

Recommended for first runs:

```json
"Sync:Enabled": "false",
"Sync:TestMode": "true"
```

TestMode fetches live Octopus and Fox ESS data but does not write schedule changes; it logs what would happen instead.

### 2. Keep secrets out of git

`local.settings.json` is listed in `.gitignore` and must **never** be committed. Only `local.settings.example.json` (with empty placeholders) is tracked.

If you ever accidentally stage `local.settings.json`, unstage it before committing:

```powershell
git reset HEAD src/SmartWattWattFunc/local.settings.json
```

### 3. Run the function

You need **two terminal windows**:

**Terminal 1 — Azurite (start this first):**

```powershell
azurite --silent
```

Keep this window open. Do not close it while testing locally.

**Terminal 2 — function host:**

```powershell
cd src/SmartWattWattFunc
func start
```

The host should start and register `EvChargeSyncTimer`.

### 4. When does it run?

The timer fires every **15 minutes** (`:00`, `:15`, `:30`, `:45`) with `RunOnStartup = false`, so nothing runs immediately when you start the host.

Options:

- **Wait** for the next 15-minute boundary
- **Run on startup** for local testing — temporarily change `EvChargeSyncTimer.cs`:

```csharp
[TimerTrigger("0 */15 * * * *", RunOnStartup = true)]
```

Revert before committing.

### 5. What to look for in the logs

On each run you should see:

- `RunSummary { ... }` — JSON summary of the run
- With TestMode enabled: `TestModeSchedulePlan` or `TestModeNoChange` with slot changes and full API-call details

To log **full HTTP request/response** details for Octopus and Fox ESS, add to `local.settings.json`:

```json
"Sync:LogHttpTraffic": "true"
```

When enabled, each API call logs two `HttpTraffic` lines in the `func start` console:

- **request** — method, URL, headers, body
- **response** — status code, headers, body

Logs include full values (tokens, API keys, signatures). Use only for local debugging — do not enable in production or shared log sinks.

### 6. Run tests

Tests do not need API keys:

```powershell
dotnet test
```

### Quick checklist

| Step | Command / action |
|---|---|
| Install tools | `winget install Microsoft.Azure.FunctionsCoreTools` |
| Install Azurite | `npm install -g azurite` |
| Start Azurite (separate terminal) | `azurite --silent` — keep that window open |
| Add API keys | Edit `src/SmartWattWattFunc/local.settings.json` |
| Start function | `cd src/SmartWattWattFunc` then `func start` |
| See output | Wait for timer, or set `RunOnStartup = true` |

### Troubleshooting

**`No connection could be made because the target machine actively refused it. (127.0.0.1:10000)`**

`local.settings.json` uses `"AzureWebJobsStorage": "UseDevelopmentStorage=true"`, which expects **Azurite** to be running locally. Start it before `func start`:

```powershell
azurite --silent
```

If you prefer not to use Azurite, replace `AzureWebJobsStorage` with a real Azure Storage account connection string (any general-purpose v2 storage account in Azure will work).

**`Fox ESS forceChargeTime/get failed: ... illegal timestamp`**

Fox ESS requires the request timestamp to be within **60 seconds of UTC**. Common causes:

1. **Windows clock not synced** — check [time.is](https://time.is/) against your PC clock. Sync time:

   ```powershell
   w32tm /resync
   ```

   Or use **Settings → Time & language → Date & time → Sync now**.

2. **Invalid or expired Fox ESS API token** — regenerate the key in the Fox ESS Cloud personal centre (API management) and update `FoxEss:ApiToken` in `local.settings.json`.

3. **Wrong device serial** — confirm `FoxEss:DeviceSerialNumber` matches your inverter `sn`.

The client also sends a browser-style `User-Agent` and `timezone` header as required by the Fox ESS Open API.

**Octopus GraphQL returns 400 (Bad Request)**

Usually an invalid `Octopus:ApiKey` or `Octopus:AccountNumber`. Verify both in your Octopus account settings. When Octopus fails, the function logs the error and still enforces the **default** Fox ESS schedule if the device is not already set to it.

## Documentation

- [SmartWattWattFunc — Specification](docs/specs/SmartWattWattFunc.md)
