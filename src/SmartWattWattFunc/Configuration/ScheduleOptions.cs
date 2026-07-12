namespace SmartWattWattFunc.Configuration;

using SmartWattWattFunc.Models;

public sealed class ScheduleOptions
{
    public string TimeZoneId { get; init; } = "Europe/London";

    public TimeOfDay DefaultSlot1Start { get; init; } = TimeOfDay.Parse("23:30");

    public TimeOfDay DefaultSlot1End { get; init; } = TimeOfDay.Parse("23:59");

    public TimeOfDay DefaultSlot2Start { get; init; } = TimeOfDay.Parse("00:00");

    public TimeOfDay DefaultSlot2End { get; init; } = TimeOfDay.Parse("05:30");

    public bool SyncEnabled { get; init; } = true;
}
