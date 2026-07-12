using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SmartWattWattFunc.Integrations.Http;

public sealed class HttpTrafficLoggingHandler(
    IConfiguration configuration,
    ILogger<HttpTrafficLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var requestBody = await ReadRequestBodyAsync(request, cancellationToken);
        logger.LogInformation(
            "HttpTraffic {Payload}",
            HttpTrafficLogFormatter.FormatRequest(
                request.Method.Method,
                request.RequestUri,
                request.Headers.Concat(GetContentHeaders(request.Content)),
                requestBody));

        var response = await base.SendAsync(request, cancellationToken);
        var responseBody = await ReadResponseBodyAsync(response, cancellationToken);

        logger.LogInformation(
            "HttpTraffic {Payload}",
            HttpTrafficLogFormatter.FormatResponse(
                (int)response.StatusCode,
                response.ReasonPhrase ?? string.Empty,
                response.Headers.Concat(GetContentHeaders(response.Content)),
                responseBody));

        return response;
    }

    private bool IsEnabled() => bool.TryParse(configuration["Sync:LogHttpTraffic"], out var enabled) && enabled;

    private static IEnumerable<KeyValuePair<string, IEnumerable<string>>> GetContentHeaders(HttpContent? content) =>
        content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>();

    private static async Task<string?> ReadRequestBodyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
        {
            return null;
        }

        var body = await request.Content.ReadAsStringAsync(cancellationToken);
        request.Content = CloneContent(body, request.Content.Headers);
        return body;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return string.Empty;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.Content = CloneContent(body, response.Content.Headers);
        return body;
    }

    private static HttpContent CloneContent(string body, HttpContentHeaders headers)
    {
        var mediaType = headers.ContentType?.MediaType ?? "application/json";
        var content = new StringContent(body, Encoding.UTF8, mediaType);

        foreach (var header in headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return content;
    }
}
