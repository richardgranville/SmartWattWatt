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

public sealed class FoxEssClient(HttpClient httpClient, FoxEssOptions options) : IFoxEssClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ForceChargeSchedule> GetForceChargeScheduleAsync(CancellationToken cancellationToken = default)
    {
        const string path = "/op/v0/device/battery/forceChargeTime/get";
        using var request = CreateRequest(HttpMethod.Get, path, query: $"sn={Uri.EscapeDataString(options.DeviceSerialNumber)}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FoxEnvelope<ForceChargeTimeResult>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Fox ESS returned an empty force charge schedule.");

        if (payload.Errno != 0)
        {
            throw new InvalidOperationException($"Fox ESS forceChargeTime/get failed: {payload.Msg}");
        }

        return MapFromFox(payload.Result!);
    }

    public async Task SetForceChargeScheduleAsync(ForceChargeSchedule schedule, CancellationToken cancellationToken = default)
    {
        const string path = "/op/v0/device/battery/forceChargeTime/set";
        var body = new ForceChargeTimeSetRequest
        {
            Sn = options.DeviceSerialNumber,
            Enable1 = schedule.Slot1.Enabled,
            Enable2 = schedule.Slot2.Enabled,
            StartTime1 = ToFoxTime(schedule.Slot1.Start),
            EndTime1 = ToFoxTime(schedule.Slot1.End),
            StartTime2 = ToFoxTime(schedule.Slot2.Start),
            EndTime2 = ToFoxTime(schedule.Slot2.End)
        };

        using var request = CreateRequest(HttpMethod.Post, path, body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FoxEnvelope<object>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Fox ESS returned an empty set response.");

        if (payload.Errno != 0)
        {
            throw new InvalidOperationException($"Fox ESS forceChargeTime/set failed: {payload.Msg}");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body = null, string? query = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var signature = ComputeSignature(path, options.ApiToken, timestamp);
        var url = options.BaseUrl.TrimEnd('/') + path + (query is null ? string.Empty : "?" + query);

        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("token", options.ApiToken);
        request.Headers.TryAddWithoutValidation("timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("signature", signature);
        request.Headers.TryAddWithoutValidation("lang", "en");
        request.Headers.TryAddWithoutValidation("User-Agent", options.UserAgent);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return request;
    }

    internal static string ComputeSignature(string path, string token, string timestamp)
    {
        var text = $"{path}\r\n{token}\r\n{timestamp}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static FoxTime ToFoxTime(TimeOfDay time) => new() { Hour = time.Hour, Minute = time.Minute };

    private static ForceChargeSchedule MapFromFox(ForceChargeTimeResult result) =>
        new(
            ScheduleMode.Default,
            new TimeSlot(ParseBool(result.Enable1), ToTimeOfDay(result.StartTime1), ToTimeOfDay(result.EndTime1)),
            new TimeSlot(ParseBool(result.Enable2), ToTimeOfDay(result.StartTime2), ToTimeOfDay(result.EndTime2)));

    private static bool ParseBool(string? value) =>
        value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private static TimeOfDay ToTimeOfDay(FoxTime? time) =>
        time is null ? new TimeOfDay(0, 0) : new TimeOfDay(time.Hour, time.Minute);

    private sealed class FoxEnvelope<T>
    {
        public int Errno { get; init; }
        public string? Msg { get; init; }
        public T? Result { get; init; }
    }

    private sealed class ForceChargeTimeResult
    {
        public string? Enable1 { get; init; }
        public FoxTime? StartTime1 { get; init; }
        public FoxTime? EndTime1 { get; init; }
        public string? Enable2 { get; init; }
        public FoxTime? StartTime2 { get; init; }
        public FoxTime? EndTime2 { get; init; }
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
    public string UserAgent { get; init; } = "SmartWattWatt/1.0";
}
