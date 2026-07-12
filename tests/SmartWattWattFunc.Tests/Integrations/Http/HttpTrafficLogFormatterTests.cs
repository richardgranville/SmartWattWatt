using SmartWattWattFunc.Integrations.Http;

namespace SmartWattWattFunc.Tests.Integrations.Http;

public sealed class HttpTrafficLogFormatterTests
{
    [Fact]
    public void FormatHeaders_PassesThroughValues()
    {
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            ["Token"] = ["secret-token"],
            ["Signature"] = ["abc123"],
            ["Lang"] = ["en"]
        };

        var formatted = HttpTrafficLogFormatter.FormatHeaders(headers);

        Assert.Equal("secret-token", formatted["Token"]);
        Assert.Equal("abc123", formatted["Signature"]);
        Assert.Equal("en", formatted["Lang"]);
    }

    [Fact]
    public void FormatRequest_IncludesRawBody()
    {
        const string body = """
            {
              "variables": {
                "apiKey": "sk_live_secret"
              }
            }
            """;

        var payload = HttpTrafficLogFormatter.FormatRequest(
            "POST",
            new Uri("https://api.octopus.energy/v1/graphql/"),
            [],
            body);

        Assert.Contains("sk_live_secret", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("[REDACTED]", payload, StringComparison.Ordinal);
    }
}
