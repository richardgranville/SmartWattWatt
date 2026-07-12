using System.Text.Json;
using System.Text.Json.Serialization;
using SmartWattWattFunc.Integrations.Octopus;
using SmartWattWattFunc.Models;
using SmartWattWattFunc.Policies;
using SmartWattWattFunc.Tests.Policies;

namespace SmartWattWattFunc.Tests.Integrations.Octopus;

public sealed class OctopusGraphQlResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void MapPlannedDispatches_ParsesSampleResponse()
    {
        const string json = """
            {
              "data": {
                "plannedDispatches": [
                  {
                    "startDt": "2026-07-12 20:07:00+00:00",
                    "endDt": "2026-07-12 20:30:00+00:00",
                    "delta": "-2.72",
                    "meta": { "source": "smart-charge", "location": null }
                  },
                  {
                    "startDt": "2026-07-12 20:30:00+00:00",
                    "endDt": "2026-07-12 21:00:00+00:00",
                    "delta": "-1.55",
                    "meta": { "source": "smart-charge", "location": null }
                  }
                ]
              }
            }
            """;

        var payload = JsonSerializer.Deserialize<GraphQlPayload>(json, JsonOptions);

        var dispatches = OctopusGraphQlClient.MapPlannedDispatches(payload?.Data?.PlannedDispatches);

        Assert.Equal(2, dispatches.Count);
        Assert.Equal(new DateTimeOffset(2026, 7, 12, 20, 7, 0, TimeSpan.Zero), dispatches[0].Start);
        Assert.Equal(new DateTimeOffset(2026, 7, 12, 20, 30, 0, TimeSpan.Zero), dispatches[0].End);
        Assert.Equal(-2.72m, dispatches[0].DeltaKwh);
        Assert.Equal("smart-charge", dispatches[0].Meta?.Source);
        Assert.Null(dispatches[0].Meta?.Location);
    }

    [Fact]
    public void MapPlannedDispatches_EmptyList_ReturnsEmpty()
    {
        var dispatches = OctopusGraphQlClient.MapPlannedDispatches([]);

        Assert.Empty(dispatches);
    }

    [Fact]
    public void MapPlannedDispatches_Null_ReturnsEmpty()
    {
        var dispatches = OctopusGraphQlClient.MapPlannedDispatches(null);

        Assert.Empty(dispatches);
    }

    [Fact]
    public void ObtainKrakenTokenResponse_DeserializesToken()
    {
        const string json = """
            {
              "data": {
                "obtainKrakenToken": {
                  "token": "eyJ.test.token"
                }
              }
            }
            """;

        var payload = JsonSerializer.Deserialize<AuthPayload>(json, JsonOptions);

        Assert.Equal("eyJ.test.token", payload?.Data?.ObtainKrakenToken?.Token);
    }

    private sealed class GraphQlPayload
    {
        public OctopusGraphQlClient.DispatchData? Data { get; init; }
    }

    private sealed class AuthPayload
    {
        public AuthData? Data { get; init; }
    }

    private sealed class AuthData
    {
        public ObtainKrakenTokenResult? ObtainKrakenToken { get; init; }
    }

    private sealed class ObtainKrakenTokenResult
    {
        public string? Token { get; init; }
    }
}

public sealed class OctopusSampleDispatchesScheduleTests
{
    private static readonly IForceChargeScheduleBuilder Builder = ScheduleTestSupport.CreateBuilder();

    private static readonly EvDispatch[] OutsideEveningDispatches =
    [
        Dispatch("2026-07-12 20:07:00+00:00", "2026-07-12 20:30:00+00:00"),
        Dispatch("2026-07-12 20:30:00+00:00", "2026-07-12 21:00:00+00:00"),
        Dispatch("2026-07-12 21:00:00+00:00", "2026-07-12 22:00:00+00:00")
    ];

    [Fact]
    public void EveningBeforeFirstOutsideDispatch_PreSchedulesFirstTwoWindows()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 19, 0);
        var desired = Builder.Build(now, OutsideEveningDispatches);

        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.PreScheduled, "21:07", "21:30", "21:30", "22:00");
    }

    [Fact]
    public void NoPlannedDispatches_ReturnsDefaultWindow()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 19, 0);
        var desired = Builder.Build(now, []);

        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
    }

    private static EvDispatch Dispatch(string startDt, string endDt) =>
        OctopusGraphQlClient.MapPlannedDispatch(new OctopusGraphQlClient.DispatchRecord
        {
            StartDt = startDt,
            EndDt = endDt,
            Delta = "-1.00",
            Meta = new OctopusGraphQlClient.DispatchMetaRecord { Source = "smart-charge" }
        });
}
