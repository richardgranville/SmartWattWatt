using System.Text.Json.Serialization;
using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Integrations.FoxEss;

internal static class FoxEssSchedulerMapper
{
    internal static ForceChargeSchedule MapFromScheduler(SchedulerGetResult result)
    {
        var forceChargeGroups = result.Groups?
            .Where(IsForceChargeWorkMode)
            .ToList() ?? [];

        return new(
            ScheduleMode.Default,
            MapForceChargeSlot(forceChargeGroups, SlotKind.Evening, result.MasterEnabled),
            MapForceChargeSlot(forceChargeGroups, SlotKind.Morning, result.MasterEnabled));
    }

    private static TimeSlot MapForceChargeSlot(
        IReadOnlyList<SchedulerGroup> groups,
        SlotKind kind,
        bool masterEnabled)
    {
        var group = kind switch
        {
            SlotKind.Evening => groups.FirstOrDefault(IsEveningForceChargeSlot)
                ?? groups.FirstOrDefault(group => !IsMorningForceChargeSlot(group)),
            SlotKind.Morning => groups.FirstOrDefault(IsMorningForceChargeSlot)
                ?? groups.FirstOrDefault(group => !IsEveningForceChargeSlot(group)),
            _ => null
        };

        if (group is null)
        {
            return TimeSlot.Disabled;
        }

        var enabled = masterEnabled && group.IsEnabled;
        return new TimeSlot(
            enabled,
            new TimeOfDay(group.StartHour, group.StartMinute),
            new TimeOfDay(group.EndHour, group.EndMinute));
    }

    private static bool IsForceChargeWorkMode(SchedulerGroup group) =>
        group.WorkMode?.StartsWith("ForceCharge", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsMorningForceChargeSlot(SchedulerGroup group) =>
        group.StartHour == 0 && group.StartMinute == 0;

    private static bool IsEveningForceChargeSlot(SchedulerGroup group) =>
        group.StartHour >= 12;

    private enum SlotKind
    {
        Evening,
        Morning
    }
}

internal sealed class SchedulerGetRequest
{
    [JsonPropertyName("deviceSN")]
    public required string DeviceSn { get; init; }
}

internal sealed class SchedulerGetResult
{
    [JsonPropertyName("enable")]
    [JsonConverter(typeof(FoxBoolJsonConverter))]
    public bool MasterEnabled { get; init; }

    public List<SchedulerGroup>? Groups { get; init; }
}

internal sealed class SchedulerGroup
{
    [JsonConverter(typeof(FoxNullableBoolJsonConverter))]
    public bool? Enable { get; init; }

    public int StartHour { get; init; }

    public int StartMinute { get; init; }

    public int EndHour { get; init; }

    public int EndMinute { get; init; }

    public string? WorkMode { get; init; }

    public bool IsEnabled => Enable ?? true;
}
