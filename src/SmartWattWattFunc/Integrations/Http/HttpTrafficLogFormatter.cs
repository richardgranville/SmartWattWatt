using System.Text.Json;

namespace SmartWattWattFunc.Integrations.Http;

internal static class HttpTrafficLogFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static string FormatRequest(string method, Uri? uri, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, string? body) =>
        JsonSerializer.Serialize(new
        {
            direction = "request",
            method,
            uri = uri?.ToString(),
            headers = FormatHeaders(headers),
            body
        }, JsonOptions);

    public static string FormatResponse(int statusCode, string reasonPhrase, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, string? body) =>
        JsonSerializer.Serialize(new
        {
            direction = "response",
            statusCode,
            reasonPhrase,
            headers = FormatHeaders(headers),
            body
        }, JsonOptions);

    internal static Dictionary<string, string> FormatHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            result[header.Key] = string.Join(", ", header.Value);
        }

        return result;
    }
}
