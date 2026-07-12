# SmartWattWatt

A monorepo application suite for coordinating **Octopus Energy** EV charging schedules with **Fox ESS** inverter work mode control.

## Status

Implementation in progress. See [SmartWattWattFunc specification](docs/specs/SmartWattWattFunc.md).

## Components

| Component | Description | Status |
|---|---|---|
| `SmartWattWattFunc` | Azure Function — progressive Force Charge staging for Octopus EV dispatches | Implemented |

## Local development

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

```powershell
cd src/SmartWattWattFunc
func start
```

### 4. Run tests

```powershell
dotnet test
```

## Documentation

- [SmartWattWattFunc — Specification](docs/specs/SmartWattWattFunc.md)
