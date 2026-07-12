namespace SmartWattWattFunc.Models;

public sealed record EvDispatch(DateTimeOffset Start, DateTimeOffset End)
{
    public bool IsPendingAt(DateTimeOffset now) => End > now;

    public bool IsActiveAt(DateTimeOffset now) => Start <= now && End > now;
}
