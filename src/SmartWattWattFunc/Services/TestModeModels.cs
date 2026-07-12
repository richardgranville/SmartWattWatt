using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Services;

public sealed record TestModeSchedulePlan(
    Guid RunId,
    DateTimeOffset TimestampUtc,
    ScheduleMode ScheduleMode,
    string Reason,
    IReadOnlyList<SlotChange> SlotChanges,
    IReadOnlyList<PlannedDispatchSummary> PlannedDispatches,
    IReadOnlyList<ApiCallDetail> ApiCalls)
{
    public bool TestMode => true;
}

public sealed record TestModeNoChange(
    Guid RunId,
    DateTimeOffset TimestampUtc,
    ScheduleMode ScheduleMode,
    string Reason,
    IReadOnlyList<ApiCallDetail> ApiCalls)
{
    public bool TestMode => true;

    public IReadOnlyList<SlotChange> SlotChanges => [];
}

public sealed record SlotChange(
    int Slot,
    string Action,
    SlotState Current,
    SlotState Desired,
    string EffectiveWhen);

public sealed record SlotState(bool Enabled, string Start, string End);

public sealed record PlannedDispatchSummary(string Start, string End, string Classification);

public sealed record ApiCallDetail(
    string Operation,
    string Method,
    string Path,
    bool Executed,
    IReadOnlyDictionary<string, string>? Query = null,
    ForceChargeSetRequestBody? RequestBody = null,
    SchedulerGetRequestBody? SchedulerGetRequestBody = null,
    IReadOnlyDictionary<string, string>? Headers = null);

public sealed record SchedulerGetRequestBody(string DeviceSN);

public sealed record ForceChargeSetRequestBody(
    string Sn,
    bool Enable1,
    bool Enable2,
    FoxTimeBody StartTime1,
    FoxTimeBody EndTime1,
    FoxTimeBody StartTime2,
    FoxTimeBody EndTime2);

public sealed record FoxTimeBody(int Hour, int Minute);
