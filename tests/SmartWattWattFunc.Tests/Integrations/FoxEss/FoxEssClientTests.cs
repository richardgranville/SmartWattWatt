using System.Security.Cryptography;
using System.Text;
using SmartWattWattFunc.Integrations.FoxEss;

namespace SmartWattWattFunc.Tests.Integrations.FoxEss;

public sealed class FoxEssClientTests
{
    [Fact]
    public void BuildLoggedHeaders_MatchesFoxEssApiShape()
    {
        var options = new FoxEssOptions
        {
            ApiToken = "api-token",
            TimeZoneId = "Australia/Sydney"
        };

        var headers = FoxEssClient.BuildLoggedHeaders(options, "1773509198752", "73d98dfa6f4a80d075078eadc9e9ae17");

        Assert.Equal("api-token", headers["Token"]);
        Assert.Equal("en", headers["Lang"]);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36", headers["User-Agent"]);
        Assert.Equal("Australia/Sydney", headers["Timezone"]);
        Assert.Equal("1773509198752", headers["Timestamp"]);
        Assert.Equal("application/json", headers["Content-Type"]);
        Assert.Equal("73d98dfa6f4a80d075078eadc9e9ae17", headers["Signature"]);
    }

    [Fact]
    public void FormatTimestampMilliseconds_MatchesPythonRoundTimeTimes1000()
    {
        var utc = DateTimeOffset.FromUnixTimeMilliseconds(1_768_282_128_342);
        var timestamp = FoxEssClient.FormatTimestampMilliseconds(utc);

        Assert.Equal("1768282128342", timestamp);
        Assert.Matches(@"^\d+$", timestamp);
    }

    [Fact]
    public void FormatTimestampMilliseconds_RoundsFractionalMilliseconds()
    {
        var utc = DateTimeOffset.UnixEpoch.AddMilliseconds(1000.6);
        var timestamp = FoxEssClient.FormatTimestampMilliseconds(utc);

        Assert.Equal("1001", timestamp);
    }

    [Fact]
    public void ComputeSignature_UsesLiteralBackslashRN_Separators()
    {
        const string path = FoxEssClient.SchedulerGetPath;
        const string token = "test-api-token";
        const string timestamp = "1783866184939";

        var signature = FoxEssClient.ComputeSignature(path, token, timestamp);

        var expected = Md5Hex($"{path}\\r\\n{token}\\r\\n{timestamp}");
        Assert.Equal(expected, signature);
    }

    [Fact]
    public void ComputeSignature_DiffersFromRealCrlfSeparator()
    {
        const string path = "/op/v0/device/list";
        const string token = "test-api-token";
        const string timestamp = "1783866184939";

        var literalSignature = FoxEssClient.ComputeSignature(path, token, timestamp);
        var crlfSignature = Md5Hex($"{path}\r\n{token}\r\n{timestamp}");

        Assert.NotEqual(crlfSignature, literalSignature);
    }

    private static string Md5Hex(string text)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
