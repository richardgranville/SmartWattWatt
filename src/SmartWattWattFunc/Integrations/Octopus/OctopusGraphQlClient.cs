using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
        mutation APIKeyAuthentication($apiKey: String!) {
          apiKeyAuthentication(apiKey: $apiKey) {
            token
          }
        }
        """;

    private const string DispatchQuery = """
        query getEvChargeData($accountNumber: String!) {
          plannedDispatches(accountNumber: $accountNumber) {
            startDt
            endDt
          }
        }
        """;

    public async Task<IReadOnlyList<EvDispatch>> GetPlannedDispatchesAsync(CancellationToken cancellationToken = default)
    {
        var token = await AuthenticateAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, options.GraphQlEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(token);
        request.Content = JsonContent.Create(new GraphQlRequest
        {
            OperationName = "getEvChargeData",
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

        return payload.Data?.PlannedDispatches?
            .Select(d => new EvDispatch(DateTimeOffset.Parse(d.StartDt!), DateTimeOffset.Parse(d.EndDt!)))
            .ToList() ?? [];
    }

    private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.GraphQlEndpoint);
        request.Content = JsonContent.Create(new GraphQlRequest
        {
            OperationName = "APIKeyAuthentication",
            Query = AuthMutation,
            Variables = new { apiKey = options.ApiKey }
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

        var token = payload.Data?.ApiKeyAuthentication?.Token;
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
        public AuthToken? ApiKeyAuthentication { get; init; }
    }

    private sealed class AuthToken
    {
        public string? Token { get; init; }
    }

    private sealed class DispatchData
    {
        public List<DispatchRecord>? PlannedDispatches { get; init; }
    }

    private sealed class DispatchRecord
    {
        public string? StartDt { get; init; }
        public string? EndDt { get; init; }
    }
}

public sealed class OctopusOptions
{
    public string GraphQlEndpoint { get; init; } = "https://api.octopus.energy/v1/graphql/";
    public string AccountNumber { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}
