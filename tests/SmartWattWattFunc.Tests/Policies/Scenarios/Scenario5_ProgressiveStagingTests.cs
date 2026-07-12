using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Tests.Policies.Scenarios;

public sealed class Scenario5_ProgressiveStagingTests
{
    private static readonly EvDispatch D1 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 1, 0, 4, 0);
    private static readonly EvDispatch D2 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 6, 0, 7, 0);
    private static readonly EvDispatch D3 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 8, 0, 10, 30);
    private static readonly EvDispatch D4 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 11, 0, 11, 30);
    private static readonly EvDispatch[] Dispatches = [D1, D2, D3, D4];
    private readonly IForceChargeScheduleBuilder _builder = ScheduleTestSupport.CreateBuilder();

    private static ForceChargeSchedule Progressive(double slot1StartH, double slot1StartM, double slot1EndH, double slot1EndM, string slot2Start, string slot2End) =>
        new(
            ScheduleMode.ProgressiveStaging,
            new TimeSlot(true, new TimeOfDay((int)slot1StartH, (int)slot1StartM), new TimeOfDay((int)slot1EndH, (int)slot1EndM)),
            new TimeSlot(true, TimeOfDay.Parse(slot2Start), TimeOfDay.Parse(slot2End)));

    [Fact]
    public void S5_T1_PreMidnight_EnforceDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 22, 0);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.OvernightAdjustedSchedule()));
    }

    [Fact]
    public void S5_T2_PreMidnight_AlreadyDefault_NoWrite()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 22, 0);
        var desired = _builder.Build(now, Dispatches);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }

    [Fact]
    public void S5_T3_InFcSlot1_EnforceDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 12, 23, 45);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.OvernightAdjustedSchedule()));
    }

    [Fact]
    public void S5_T4_InFcAndInsideDispatch_StageD2InSlot1()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 1, 15);
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.ProgressiveStaging, "06:00", "07:00", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, ScheduleTestSupport.DefaultSchedule()));
    }

    [Fact]
    public void S5_T5_InFc_StageD3InSlot2()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 4, 15);
        var current = Progressive(6, 0, 7, 0, "00:00", "05:30");
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.ProgressiveStaging, "06:00", "07:00", "08:00", "10:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, current));
    }

    [Fact]
    public void S5_T6_AfterFc_StageD4InSlot1()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 6, 15);
        var current = Progressive(6, 0, 7, 0, "08:00", "10:30");
        var desired = _builder.Build(now, Dispatches);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.ProgressiveStaging, "11:00", "11:30", "08:00", "10:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, current));
    }

    [Fact]
    public void S5_T7_Daytime_RestoreSlot2ToDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 8, 15);
        var current = Progressive(11, 0, 11, 30, "08:00", "10:30");
        var desired = _builder.Build(now, [D3, D4]);
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.ProgressiveStaging, "11:00", "11:30", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, current));
    }

    [Fact]
    public void S5_T8_Daytime_NoChanges()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 10, 45);
        var current = Progressive(11, 0, 11, 30, "00:00", "05:30");
        var desired = _builder.Build(now, [D4]);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, current));
    }

    [Fact]
    public void S5_T9_DuringD4_NoChanges()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 11, 15);
        var current = Progressive(11, 0, 11, 30, "00:00", "05:30");
        var desired = _builder.Build(now, [D4]);
        Assert.False(ScheduleTestSupport.ShouldWrite(desired, current));
    }

    [Fact]
    public void S5_T10_AfterAllComplete_RestoreDefault()
    {
        var now = ScheduleTestSupport.AtLocal(2026, 7, 13, 11, 45);
        var current = Progressive(11, 0, 11, 30, "00:00", "05:30");
        var desired = _builder.Build(now, Array.Empty<EvDispatch>());
        ScheduleTestSupport.AssertSchedule(desired, ScheduleMode.Default, "23:30", "23:59", "00:00", "05:30");
        Assert.True(ScheduleTestSupport.ShouldWrite(desired, current));
    }
}
