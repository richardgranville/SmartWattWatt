using System.Text.Json;
using System.Text.Json.Serialization;
using SmartWattWattFunc.Integrations.FoxEss;
using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Tests.Integrations.FoxEss;

public sealed class FoxEssSchedulerResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void SchedulerGet_MapsForceChargeGroupsByTime_NotArrayIndex()
    {
        const string json = """
            {
              "errno": 0,
              "msg": "Operation successful",
              "result": {
                "enable": 1,
                "maxGroupCount": 96,
                "groups": [
                  {
                    "endHour": 5,
                    "workMode": "ForceCharge",
                    "startHour": 0,
                    "startMinute": 0,
                    "endMinute": 30
                  },
                  {
                    "endHour": 23,
                    "workMode": "ForceCharge",
                    "startHour": 23,
                    "startMinute": 30,
                    "endMinute": 59
                  },
                  {
                    "endHour": 23,
                    "workMode": "SelfUse",
                    "startHour": 0,
                    "startMinute": 0,
                    "endMinute": 59
                  }
                ]
              }
            }
            """;

        var payload = JsonSerializer.Deserialize<SchedulerGetEnvelope>(json, JsonOptions);

        Assert.NotNull(payload?.Result);
        var schedule = FoxEssSchedulerMapper.MapFromScheduler(payload!.Result!);

        Assert.True(schedule.Slot1.Enabled);
        Assert.Equal(new TimeOfDay(23, 30), schedule.Slot1.Start);
        Assert.Equal(new TimeOfDay(23, 59), schedule.Slot1.End);
        Assert.True(schedule.Slot2.Enabled);
        Assert.Equal(new TimeOfDay(0, 0), schedule.Slot2.Start);
        Assert.Equal(new TimeOfDay(5, 30), schedule.Slot2.End);
    }

    [Fact]
    public void SchedulerGet_IgnoresNonForceChargeGroups()
    {
        const string json = """
            {
              "errno": 0,
              "msg": "Operation successful",
              "result": {
                "enable": 1,
                "groups": [
                  {
                    "startHour": 0,
                    "startMinute": 0,
                    "endHour": 23,
                    "endMinute": 59,
                    "workMode": "SelfUse"
                  },
                  {
                    "enable": 0,
                    "startHour": 0,
                    "startMinute": 0,
                    "endHour": 5,
                    "endMinute": 29,
                    "workMode": "ForceCharge"
                  },
                  {
                    "enable": 1,
                    "startHour": 23,
                    "startMinute": 30,
                    "endHour": 23,
                    "endMinute": 59,
                    "workMode": "ForceCharge"
                  }
                ],
                "maxGroupCount": 8
              }
            }
            """;

        var payload = JsonSerializer.Deserialize<SchedulerGetEnvelope>(json, JsonOptions);
        var schedule = FoxEssSchedulerMapper.MapFromScheduler(payload!.Result!);

        Assert.True(schedule.Slot1.Enabled);
        Assert.Equal(new TimeOfDay(23, 30), schedule.Slot1.Start);
        Assert.False(schedule.Slot2.Enabled);
        Assert.Equal(new TimeOfDay(0, 0), schedule.Slot2.Start);
        Assert.Equal(new TimeOfDay(5, 29), schedule.Slot2.End);
    }

    [Fact]
    public void SchedulerGet_MasterSwitchOff_DisablesBothSlots()
    {
        const string json = """
            {
              "errno": 0,
              "msg": "Operation successful",
              "result": {
                "enable": 0,
                "groups": [
                  {
                    "startHour": 0,
                    "startMinute": 0,
                    "endHour": 5,
                    "endMinute": 30,
                    "workMode": "ForceCharge"
                  },
                  {
                    "startHour": 23,
                    "startMinute": 30,
                    "endHour": 23,
                    "endMinute": 59,
                    "workMode": "ForceCharge"
                  }
                ]
              }
            }
            """;

        var payload = JsonSerializer.Deserialize<SchedulerGetEnvelope>(json, JsonOptions);
        var schedule = FoxEssSchedulerMapper.MapFromScheduler(payload!.Result!);

        Assert.False(schedule.Slot1.Enabled);
        Assert.False(schedule.Slot2.Enabled);
    }

    [Fact]
    public void SchedulerGet_MissingForceChargeGroups_ReturnsDisabledSlots()
    {
        const string json = """
            {
              "errno": 0,
              "msg": "Operation successful",
              "result": {
                "enable": 1,
                "groups": [
                  {
                    "startHour": 0,
                    "startMinute": 0,
                    "endHour": 23,
                    "endMinute": 59,
                    "workMode": "SelfUse"
                  }
                ]
              }
            }
            """;

        var payload = JsonSerializer.Deserialize<SchedulerGetEnvelope>(json, JsonOptions);
        var schedule = FoxEssSchedulerMapper.MapFromScheduler(payload!.Result!);

        Assert.False(schedule.Slot1.Enabled);
        Assert.False(schedule.Slot2.Enabled);
    }

    private sealed class SchedulerGetEnvelope
    {
        public int Errno { get; init; }
        public string? Msg { get; init; }
        public SchedulerGetResult? Result { get; init; }
    }
}
