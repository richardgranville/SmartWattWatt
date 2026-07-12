using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Tests.Policies.Scenarios;

public sealed class Scenario2_AllInsideDefaultTests
{
    private static readonly EvDispatch D1 = ScheduleTestSupport.DispatchLocal(2026, 7, 12, 23, 0, 1, 0, endDayOffset: 1);
    private static readonly EvDispatch D2 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 2, 0, 3, 0);
    private static readonly EvDispatch D3 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 4, 0, 5, 0);
    private static readonly EvDispatch[] Dispatches = [D1, D2, D3];
    private readonly IForceChargeScheduleBuilder _builder = ScheduleTestSupport.CreateBuilder();

    [Fact]
    public void S2_T1_Evening_EnforceDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 22, 0);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.OvernightAdjustedSchedule()));
    }

    [Fact]
    public void S2_T2_Evening_AlreadyDefault_NoWrite()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 22, 0);
        var desired = _builder.Build(now, Dispatches);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }

    [Fact]
    public void S2_T3_AfterMidnight_HoldDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 0, 30);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
    }

    [Fact]
    public void S2_T4_DuringSecondDispatch_HoldDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 2, 15);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
    }

    [Fact]
    public void S2_T5_DuringThirdDispatch_HoldDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 4, 30);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
    }

    [Fact]
    public void S2_T6_AfterAllComplete_DefaultUnchanged()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 5, 15);
        var desired = _builder.Build(now, Dispatches);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }
}
