namespace SmartWattWattFunc.Models;

public sealed record EvDispatchMeta(string? Source, string? Location);

public sealed record EvDispatch(
    DateTimeOffset Start,
    DateTimeOffset End,
    decimal? DeltaKwh = null,
    EvDispatchMeta? Meta = null)
{
    public bool IsPendingAt(DateTimeOffset now) => End > now;

    public bool IsActiveAt(DateTimeOffset now) => Start <= now && End > now;
}
