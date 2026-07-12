using SmartWattWattFunc.Configuration;
using SmartWattWattFunc.Models;
using SmartWattWattFunc.Policies;

namespace SmartWattWattFunc.Tests.Policies;

internal static class ScheduleTestSupport
{
    public static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static readonly ScheduleOptions Options = new()
    {
        TimeZoneId = "Europe/London"
    };

    public static IForceChargeScheduleBuilder CreateBuilder() => new ForceChargeScheduleBuilder(Options);

    public static DateTimeOffset AtLocal(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, London.GetUtcOffset(local));
    }

    public static EvDispatch DispatchLocal(
        int year,
        int month,
        int day,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        int endDayOffset = 0)
    {
        var start = AtLocal(year, month, day, startHour, startMinute);
        var end = AtLocal(year, month, day + endDayOffset, endHour, endMinute);
        return new EvDispatch(start, end);
    }

    public static ForceChargeSchedule DefaultSchedule() =>
        new(
            ScheduleMode.Default,
            new TimeSlot(true, TimeOfDay.Parse("23:30"), TimeOfDay.Parse("23:59")),
            new TimeSlot(true, TimeOfDay.Parse("00:00"), TimeOfDay.Parse("05:30")));

    public static ForceChargeSchedule OvernightAdjustedSchedule() =>
        new(
            ScheduleMode.OvernightAdjusted,
            new TimeSlot(true, TimeOfDay.Parse("07:30"), TimeOfDay.Parse("08:30")),
            new TimeSlot(true, TimeOfDay.Parse("00:00"), TimeOfDay.Parse("05:30")));

    public static ForceChargeSchedule PreScheduledSchedule() =>
        new(
            ScheduleMode.PreScheduled,
            new TimeSlot(true, TimeOfDay.Parse("14:00"), TimeOfDay.Parse("15:00")),
            new TimeSlot(true, TimeOfDay.Parse("18:00"), TimeOfDay.Parse("19:00")));

    public static void AssertSchedule(
        ForceChargeSchedule actual,
        ScheduleMode mode,
        string slot1Start,
        string slot1End,
        string slot2Start,
        string slot2End)
    {
        Assert.Equal(mode, actual.Mode);
        Assert.Equal(slot1Start, actual.Slot1.Start.ToString());
        Assert.Equal(slot1End, actual.Slot1.End.ToString());
        Assert.Equal(slot2Start, actual.Slot2.Start.ToString());
        Assert.Equal(slot2End, actual.Slot2.End.ToString());
        Assert.True(actual.Slot1.Enabled);
        Assert.True(actual.Slot2.Enabled);
    }

    public static bool ShouldWrite(ForceChargeSchedule desired, ForceChargeSchedule current) =>
        desired.RequiresWriteComparedTo(current);
}
