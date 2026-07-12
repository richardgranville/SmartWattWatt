using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Integrations.FoxEss;

public interface IFoxEssClient
{
    Task<ForceChargeSchedule> GetForceChargeScheduleAsync(CancellationToken cancellationToken = default);

    Task SetForceChargeScheduleAsync(ForceChargeSchedule schedule, CancellationToken cancellationToken = default);
}

public sealed class FoxEssClient(HttpClient httpClient, FoxEssOptions options, TimeProvider timeProvider) : IFoxEssClient
{
    internal const string SchedulerGetPath = "/op/v3/device/scheduler/get";
    internal const string ForceChargeSetPath = "/op/v0/device/battery/forceChargeTime/set";

    private const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string JsonMediaType = "application/json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ForceChargeSchedule> GetForceChargeScheduleAsync(CancellationToken cancellationToken = default)
    {
        var serialNumber = RequireDeviceSerialNumber();
        var body = new SchedulerGetRequest { DeviceSn = serialNumber };
        using var request = CreateRequest(HttpMethod.Post, SchedulerGetPath, body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FoxEnvelope<SchedulerGetResult>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Fox ESS returned an empty scheduler schedule.");

        if (payload.Errno != 0)
        {
            throw new InvalidOperationException($"Fox ESS scheduler/get failed: {payload.Msg}");
        }

        return FoxEssSchedulerMapper.MapFromScheduler(payload.Result!);
    }

    public async Task SetForceChargeScheduleAsync(ForceChargeSchedule schedule, CancellationToken cancellationToken = default)
    {
        var serialNumber = RequireDeviceSerialNumber();
        var body = new ForceChargeTimeSetRequest
        {
            Sn = serialNumber,
            Enable1 = schedule.Slot1.Enabled,
            Enable2 = schedule.Slot2.Enabled,
            StartTime1 = ToFoxTime(schedule.Slot1.Start),
            EndTime1 = ToFoxTime(schedule.Slot1.End),
            StartTime2 = ToFoxTime(schedule.Slot2.Start),
            EndTime2 = ToFoxTime(schedule.Slot2.End)
        };

        using var request = CreateRequest(HttpMethod.Post, ForceChargeSetPath, body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FoxEnvelope<object>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Fox ESS returned an empty set response.");

        if (payload.Errno != 0)
        {
            throw new InvalidOperationException($"Fox ESS forceChargeTime/set failed: {payload.Msg}");
        }
    }

    private string RequireDeviceSerialNumber()
    {
        if (string.IsNullOrWhiteSpace(options.DeviceSerialNumber))
        {
            throw new InvalidOperationException(
                "FoxEss:DeviceSerialNumber is required for Fox ESS API calls.");
        }

        return options.DeviceSerialNumber;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body = null, string? query = null)
    {
        var timestamp = FormatTimestampMilliseconds(timeProvider.GetUtcNow());
        var signature = ComputeSignature(path, options.ApiToken, timestamp);
        var url = options.BaseUrl.TrimEnd('/') + path + (query is null ? string.Empty : "?" + query);

        var request = new HttpRequestMessage(method, url);
        AddFoxEssHeaders(request, timestamp, signature);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, JsonMediaType);
        }
        else
        {
            request.Headers.TryAddWithoutValidation("Content-Type", JsonMediaType);
        }

        return request;
    }

    internal static void AddFoxEssHeaders(HttpRequestMessage request, FoxEssOptions options, string timestamp, string signature)
    {
        request.Headers.TryAddWithoutValidation("Token", options.ApiToken);
        request.Headers.TryAddWithoutValidation("Lang", "en");
        request.Headers.TryAddWithoutValidation("User-Agent", ResolveUserAgent(options));
        request.Headers.TryAddWithoutValidation("Timezone", options.TimeZoneId);
        request.Headers.TryAddWithoutValidation("Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("Signature", signature);
    }

    internal static string ResolveUserAgent(FoxEssOptions options) =>
        string.IsNullOrWhiteSpace(options.UserAgent) ? DefaultUserAgent : options.UserAgent;

    private void AddFoxEssHeaders(HttpRequestMessage request, string timestamp, string signature) =>
        AddFoxEssHeaders(request, options, timestamp, signature);

    internal static IReadOnlyDictionary<string, string> BuildLoggedHeaders(
        FoxEssOptions options,
        string timestamp,
        string signature) =>
        new Dictionary<string, string>
        {
            ["Token"] = options.ApiToken,
            ["Lang"] = "en",
            ["User-Agent"] = ResolveUserAgent(options),
            ["Timezone"] = options.TimeZoneId,
            ["Timestamp"] = timestamp,
            ["Content-Type"] = JsonMediaType,
            ["Signature"] = signature
        };

    // Equivalent to Python: str(round(time.time() * 1000))
    internal static string FormatTimestampMilliseconds(DateTimeOffset utcNow)
    {
        var milliseconds = Math.Round(
            (utcNow - DateTimeOffset.UnixEpoch).TotalMilliseconds,
            MidpointRounding.ToEven);

        return ((long)milliseconds).ToString(CultureInfo.InvariantCulture);
    }

    internal static string ComputeSignature(string path, string token, string timestamp)
    {
        // Fox ESS expects the literal characters \ r \ n between fields, not a CRLF byte sequence.
        var text = $"{path}\\r\\n{token}\\r\\n{timestamp}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static FoxTime ToFoxTime(TimeOfDay time) => new() { Hour = time.Hour, Minute = time.Minute };

    private sealed class FoxEnvelope<T>
    {
        public int Errno { get; init; }
        public string? Msg { get; init; }
        public T? Result { get; init; }
    }

    private sealed class ForceChargeTimeSetRequest
    {
        public required string Sn { get; init; }
        public required bool Enable1 { get; init; }
        public required bool Enable2 { get; init; }
        public required FoxTime StartTime1 { get; init; }
        public required FoxTime EndTime1 { get; init; }
        public required FoxTime StartTime2 { get; init; }
        public required FoxTime EndTime2 { get; init; }
    }

    private sealed class FoxTime
    {
        public int Hour { get; init; }
        public int Minute { get; init; }
    }
}

public sealed class FoxEssOptions
{
    public string BaseUrl { get; init; } = "https://www.foxesscloud.com";
    public string DeviceSerialNumber { get; init; } = string.Empty;
    public string ApiToken { get; init; } = string.Empty;
    public string TimeZoneId { get; init; } = "Europe/London";
    public string UserAgent { get; init; } = string.Empty;
}
