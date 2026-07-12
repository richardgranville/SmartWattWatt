using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartWattWattFunc.Configuration;
using SmartWattWattFunc.Integrations.FoxEss;
using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Services;

public sealed class TestModeLogger(
    FoxEssOptions foxEssOptions,
    ScheduleOptions scheduleOptions,
    ILogger<TestModeLogger> logger) : ITestModeLogger
{
    private const string Redacted = "[REDACTED]";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public void LogSchedulePlan(TestModeSchedulePlan plan) =>
        logger.LogInformation("TestModeSchedulePlan {Payload}", JsonSerializer.Serialize(plan, JsonOptions));

    public void LogNoChange(TestModeNoChange plan) =>
        logger.LogInformation("TestModeNoChange {Payload}", JsonSerializer.Serialize(plan, JsonOptions));

    public TestModeSchedulePlan CreateSchedulePlan(
        Guid runId,
        DateTimeOffset timestampUtc,
        ForceChargeSchedule desired,
        ForceChargeSchedule current,
        IReadOnlyList<EvDispatch> dispatches) =>
        new(
            runId,
            timestampUtc,
            desired.Mode,
            BuildReason(desired.Mode),
            BuildSlotChanges(current, desired),
            BuildPlannedDispatches(dispatches),
            BuildApiCalls(desired, getExecuted: true, includeSetCall: true));

    public TestModeNoChange CreateNoChangePlan(
        Guid runId,
        DateTimeOffset timestampUtc,
        ForceChargeSchedule desired) =>
        new(
            runId,
            timestampUtc,
            desired.Mode,
            "Desired schedule already matches Fox ESS",
            BuildApiCalls(desired, getExecuted: true, includeSetCall: false));

    private IReadOnlyList<SlotChange> BuildSlotChanges(ForceChargeSchedule current, ForceChargeSchedule desired) =>
    [
        BuildSlotChange(1, current.Slot1, desired.Slot1),
        BuildSlotChange(2, current.Slot2, desired.Slot2)
    ];

    private static SlotChange BuildSlotChange(int slot, TimeSlot current, TimeSlot desired)
    {
        var action = current.Matches(desired) ? "Unchanged" : "Update";
        return new SlotChange(
            slot,
            action,
            ToSlotState(current),
            ToSlotState(desired),
            "Immediate on next Fox ESS set call");
    }

    private static SlotState ToSlotState(TimeSlot slot) =>
        new(slot.Enabled, slot.Start.ToString(), slot.End.ToString());

    private IReadOnlyList<PlannedDispatchSummary> BuildPlannedDispatches(IReadOnlyList<EvDispatch> dispatches) =>
        dispatches
            .Select(d => new PlannedDispatchSummary(
                d.Start.ToString("o"),
                d.End.ToString("o"),
                ClassifyDispatch(d)))
            .ToList();

    private string ClassifyDispatch(EvDispatch dispatch)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(scheduleOptions.TimeZoneId);
        var endLocal = TimeZoneInfo.ConvertTime(dispatch.End, timeZone);
        var defaultEnd = scheduleOptions.DefaultSlot2End.ToTimeSpan();
        return endLocal.TimeOfDay > defaultEnd ? "OutsideDefault" : "InsideDefault";
    }

    private static string BuildReason(ScheduleMode mode) =>
        mode switch
        {
            ScheduleMode.Default => "Enforce default Force Charge windows",
            ScheduleMode.OvernightAdjusted => "After 00:00 — repurposed slot 1 for outside-default dispatch",
            ScheduleMode.PreScheduled => "Daytime gap — pre-scheduled upcoming dispatches",
            ScheduleMode.ProgressiveStaging => "Progressive staging of outside-default dispatches",
            _ => "Schedule update required"
        };

    private IReadOnlyList<ApiCallDetail> BuildApiCalls(
        ForceChargeSchedule desired,
        bool getExecuted,
        bool includeSetCall)
    {
        var calls = new List<ApiCallDetail>
        {
            new(
                "GetForceChargeSchedule",
                "GET",
                "/op/v0/device/battery/forceChargeTime/get",
                getExecuted,
                new Dictionary<string, string>
                {
                    ["sn"] = foxEssOptions.DeviceSerialNumber
                })
        };

        if (includeSetCall)
        {
            calls.Add(new ApiCallDetail(
                "SetForceChargeSchedule",
                "POST",
                "/op/v0/device/battery/forceChargeTime/set",
                false,
                RequestBody: BuildSetRequestBody(desired),
                Headers: new Dictionary<string, string>
                {
                    ["token"] = Redacted,
                    ["timestamp"] = Redacted,
                    ["signature"] = Redacted,
                    ["lang"] = "en",
                    ["User-Agent"] = foxEssOptions.UserAgent
                }));
        }

        return calls;
    }

    private ForceChargeSetRequestBody BuildSetRequestBody(ForceChargeSchedule schedule) =>
        new(
            foxEssOptions.DeviceSerialNumber,
            schedule.Slot1.Enabled,
            schedule.Slot2.Enabled,
            ToFoxTime(schedule.Slot1.Start),
            ToFoxTime(schedule.Slot1.End),
            ToFoxTime(schedule.Slot2.Start),
            ToFoxTime(schedule.Slot2.End));

    private static FoxTimeBody ToFoxTime(TimeOfDay time) => new(time.Hour, time.Minute);
}
