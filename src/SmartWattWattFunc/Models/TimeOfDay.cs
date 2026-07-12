namespace SmartWattWattFunc.Models;

public readonly record struct TimeOfDay(int Hour, int Minute)
{
    public static TimeOfDay Parse(string value)
    {
        var parts = value.Split(':');
        return new TimeOfDay(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public TimeSpan ToTimeSpan() => new(Hour, Minute, 0);

    public override string ToString() => $"{Hour:D2}:{Minute:D2}";
}
