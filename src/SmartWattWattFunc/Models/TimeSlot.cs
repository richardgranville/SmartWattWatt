namespace SmartWattWattFunc.Models;

public sealed record TimeSlot(bool Enabled, TimeOfDay Start, TimeOfDay End)
{
    public static TimeSlot Disabled { get; } = new(false, new TimeOfDay(0, 0), new TimeOfDay(0, 0));

    public static TimeSlot FromDispatch(DateTimeOffset start, DateTimeOffset end) =>
        new(true, new TimeOfDay(start.Hour, start.Minute), new TimeOfDay(end.Hour, end.Minute));

    public bool Matches(TimeSlot other) =>
        Enabled == other.Enabled &&
        Start == other.Start &&
        End == other.End;
}
