using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Integrations.Octopus;

public interface IOctopusGraphQlClient
{
    Task<IReadOnlyList<EvDispatch>> GetPlannedDispatchesAsync(CancellationToken cancellationToken = default);
}

public sealed class OctopusGraphQlClient(HttpClient httpClient, OctopusOptions options) : IOctopusGraphQlClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string AuthMutation = """
        mutation obtainKrakenToken($APIKey: String!) {
          obtainKrakenToken(input: { APIKey: $APIKey }) {
            token
          }
        }
        """;

    private const string DispatchQuery = """
        query getDispatches($accountNumber: String!) {
          plannedDispatches(accountNumber: $accountNumber) {
            startDt
            endDt
            delta
            meta {
              source
              location
            }
          }
        }
        """;

    public async Task<IReadOnlyList<EvDispatch>> GetPlannedDispatchesAsync(CancellationToken cancellationToken = default)
    {
        var token = await AuthenticateAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, options.GraphQlEndpoint);
        request.Headers.TryAddWithoutValidation("Authorization", token);
        request.Content = JsonContent.Create(new GraphQlRequest
        {
            OperationName = "getDispatches",
            Query = DispatchQuery,
            Variables = new { accountNumber = options.AccountNumber }
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GraphQlResponse<DispatchData>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Octopus GraphQL returned an empty response.");

        if (payload.Errors is { Count: > 0 })
        {
            var message = string.Join("; ", payload.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Octopus GraphQL query failed: {message}");
        }

        return MapPlannedDispatches(payload.Data?.PlannedDispatches);
    }

    internal static IReadOnlyList<EvDispatch> MapPlannedDispatches(IEnumerable<DispatchRecord>? records) =>
        records?
            .Select(MapPlannedDispatch)
            .ToList() ?? [];

    internal static EvDispatch MapPlannedDispatch(DispatchRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.StartDt) || string.IsNullOrWhiteSpace(record.EndDt))
        {
            throw new InvalidOperationException("Octopus planned dispatch is missing startDt or endDt.");
        }

        return new EvDispatch(
            DateTimeOffset.Parse(record.StartDt, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(record.EndDt, CultureInfo.InvariantCulture),
            ParseDelta(record.Delta),
            record.Meta is null
                ? null
                : new EvDispatchMeta(record.Meta.Source, record.Meta.Location));
    }

    private static decimal? ParseDelta(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.GraphQlEndpoint);
        request.Content = JsonContent.Create(new GraphQlRequest
        {
            OperationName = "obtainKrakenToken",
            Query = AuthMutation,
            Variables = new Dictionary<string, string> { ["APIKey"] = options.ApiKey }
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GraphQlResponse<AuthData>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Octopus authentication returned an empty response.");

        if (payload.Errors is { Count: > 0 })
        {
            var message = string.Join("; ", payload.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Octopus authentication failed: {message}");
        }

        var token = payload.Data?.ObtainKrakenToken?.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Octopus authentication did not return a token.");
        }

        return token;
    }

    private sealed class GraphQlRequest
    {
        public string? OperationName { get; init; }
        public required string Query { get; init; }
        public object? Variables { get; init; }
    }

    private sealed class GraphQlResponse<T>
    {
        public T? Data { get; init; }
        public List<GraphQlError>? Errors { get; init; }
    }

    private sealed class GraphQlError
    {
        public string? Message { get; init; }
    }

    private sealed class AuthData
    {
        public ObtainKrakenTokenResult? ObtainKrakenToken { get; init; }
    }

    private sealed class ObtainKrakenTokenResult
    {
        public string? Token { get; init; }
    }

    internal sealed class DispatchData
    {
        public List<DispatchRecord>? PlannedDispatches { get; init; }
    }

    internal sealed class DispatchRecord
    {
        public string? StartDt { get; init; }
        public string? EndDt { get; init; }
        public string? Delta { get; init; }
        public DispatchMetaRecord? Meta { get; init; }
    }

    internal sealed class DispatchMetaRecord
    {
        public string? Source { get; init; }
        public string? Location { get; init; }
    }
}

public sealed class OctopusOptions
{
    public string GraphQlEndpoint { get; init; } = "https://api.octopus.energy/v1/graphql/";
    public string AccountNumber { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}
