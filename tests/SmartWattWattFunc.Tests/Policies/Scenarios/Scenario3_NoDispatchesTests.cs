using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Tests.Policies.Scenarios;

public sealed class Scenario3_NoDispatchesTests
{
    private readonly IForceChargeScheduleBuilder _builder = ScheduleTestSupport.CreateBuilder();

    [Fact]
    public void S3_T1_Evening_EnforceDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 22, 0);
        var desired = _builder.Build(now, Array.Empty<EvDispatch>());
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.OvernightAdjustedSchedule()));
    }

    [Fact]
    public void S3_T2_Evening_AlreadyDefault_NoWrite()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 22, 0);
        var desired = _builder.Build(now, Array.Empty<EvDispatch>());
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }

    [Fact]
    public void S3_T3_AfterMidnight_RestoreDefaultFromAdjusted()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 3, 0);
        var desired = _builder.Build(now, Array.Empty<EvDispatch>());
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.OvernightAdjustedSchedule()));
    }

    [Fact]
    public void S3_T4_Daytime_RestoreDefaultFromAdjusted()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 14, 0);
        var desired = _builder.Build(now, Array.Empty<EvDispatch>());
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.OvernightAdjustedSchedule()));
    }

    [Fact]
    public void S3_T5_Daytime_AlreadyDefault_NoWrite()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 14, 0);
        var desired = _builder.Build(now, Array.Empty<EvDispatch>());
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }
}
