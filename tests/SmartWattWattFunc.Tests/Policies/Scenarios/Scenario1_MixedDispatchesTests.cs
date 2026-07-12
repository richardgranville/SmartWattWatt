using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Tests.Policies.Scenarios;

public sealed class Scenario1_MixedDispatchesTests
{
    private static readonly EvDispatch D1 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 3, 0, 5, 30);
    private static readonly EvDispatch D2 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 7, 30, 8, 30);
    private static readonly EvDispatch[] Dispatches = [D1, D2];
    private readonly IForceChargeScheduleBuilder _builder = ScheduleTestSupport.CreateBuilder();

    [Fact]
    public void S1_T1_Evening_EnforceDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 22, 0);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.OvernightAdjustedSchedule()));
    }

    [Fact]
    public void S1_T2_Evening_AlreadyDefault_NoWrite()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 22, 15);
        var desired = _builder.Build(now, Dispatches);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }

    [Fact]
    public void S1_T3_BeforeMidnight_HoldDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 23, 45);
        var desired = _builder.Build(now, Dispatches);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }

    [Fact]
    public void S1_T4_AfterMidnight_RepurposeSlot1()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 0, 15);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.OvernightAdjusted, "07:30", "08:30", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }

    [Fact]
    public void S1_T5_AfterMidnight_AlreadyAdjusted_NoWrite()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 0, 15);
        var desired = _builder.Build(now, Dispatches);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.OvernightAdjustedSchedule()));
    }

    [Fact]
    public void S1_T6_DuringInsideDefault_HoldAdjusted()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 3, 15);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.OvernightAdjusted, "07:30", "08:30", "00:00", "05:30");
    }

    [Fact]
    public void S1_T7_BetweenDispatches_HoldAdjusted()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 6, 0);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.OvernightAdjusted, "07:30", "08:30", "00:00", "05:30");
    }

    [Fact]
    public void S1_T8_AfterAllComplete_RestoreDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 8, 35);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.OvernightAdjustedSchedule()));
    }

    [Fact]
    public void S1_T9_AfterRestore_HoldDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 8, 45);
        var desired = _builder.Build(now, Array.Empty<EvDispatch>());
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }
}
