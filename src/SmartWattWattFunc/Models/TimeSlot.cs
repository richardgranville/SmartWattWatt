namespace SmartWattWattFunc.Models;

public sealed record TimeSlot(bool Enabled, TimeOfDay Start, TimeOfDay End)
{
    public static TimeSlot Disabled { get; } = new(false, new TimeOfDay(0, 0), new TimeOfDay(0, 0));

    public static TimeSlot FromDispatch(DateTimeOffset start, DateTimeOffset end, TimeZoneInfo timeZone)
    {
        var startLocal = TimeZoneInfo.ConvertTime(start, timeZone);
        var endLocal = TimeZoneInfo.ConvertTime(end, timeZone);
        return new(true, new TimeOfDay(startLocal.Hour, startLocal.Minute), new TimeOfDay(endLocal.Hour, endLocal.Minute));
    }

    public bool Matches(TimeSlot other) =>
        Enabled == other.Enabled &&
        Start == other.Start &&
        End == other.End;
}
