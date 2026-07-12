using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Tests.Policies.Scenarios;

public sealed class Scenario4_DaytimePreScheduleTests
{
    private static readonly EvDispatch D1 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 14, 0, 15, 0);
    private static readonly EvDispatch D2 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 18, 0, 19, 0);
    private static readonly EvDispatch[] Dispatches = [D1, D2];
    private readonly IForceChargeScheduleBuilder _builder = ScheduleTestSupport.CreateBuilder();

    [Fact]
    public void S4_T1_DaytimeIdle_PreScheduleTwoDispatches()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 10, 0);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.PreScheduled, "14:00", "15:00", "18:00", "19:00");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }

    [Fact]
    public void S4_T2_DaytimeIdle_AlreadyPreScheduled_NoWrite()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 10, 15);
        var current = new ForceChargeSchedule(
            ScheduleMode.PreScheduled,
            new TimeSlot(true, TimeOfDay.Parse("14:00"), TimeOfDay.Parse("15:00")),
            new TimeSlot(true, TimeOfDay.Parse("18:00"), TimeOfDay.Parse("19:00")));
        var desired = _builder.Build(now, Dispatches);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, current));
    }

    [Fact]
    public void S4_T3_SingleDispatch_Slot2Default()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 10, 0);
        var desired = _builder.Build(now, [D1]);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.PreScheduled, "14:00", "15:00", "00:00", "05:30");
    }

    [Fact]
    public void S4_T4_UpdatedDispatches_RewriteSchedule()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 11, 0);
        var updatedD1 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 13, 0, 14, 0);
        var current = new ForceChargeSchedule(
            ScheduleMode.PreScheduled,
            new TimeSlot(true, TimeOfDay.Parse("14:00"), TimeOfDay.Parse("15:00")),
            new TimeSlot(true, TimeOfDay.Parse("18:00"), TimeOfDay.Parse("19:00")));
        var desired = _builder.Build(now, [updatedD1, D2]);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.PreScheduled, "13:00", "14:00", "18:00", "19:00");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, current));
    }

    [Fact]
    public void S4_T5_BetweenWindows_HoldPreScheduled()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 15, 30);
        var current = new ForceChargeSchedule(
            ScheduleMode.PreScheduled,
            new TimeSlot(true, TimeOfDay.Parse("14:00"), TimeOfDay.Parse("15:00")),
            new TimeSlot(true, TimeOfDay.Parse("18:00"), TimeOfDay.Parse("19:00")));
        var desired = _builder.Build(now, Dispatches);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, current));
    }

    [Fact]
    public void S4_T6_PreMidnightEvening_DefaultNotPreScheduled()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 22, 0);
        var tomorrowDispatches = new[]
        {
            ScheduleTestSupport.DispatchLocal(2026, 7, 13, 14, 0, 15, 0),
            ScheduleTestSupport.DispatchLocal(2026, 7, 13, 18, 0, 19, 0)
        };
        var desired = _builder.Build(now, tomorrowDispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
    }
}
