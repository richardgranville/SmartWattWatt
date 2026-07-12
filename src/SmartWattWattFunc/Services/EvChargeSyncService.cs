using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartWattWattFunc.Configuration;
using SmartWattWattFunc.Integrations.FoxEss;
using SmartWattWattFunc.Integrations.Octopus;
using SmartWattWattFunc.Models;
using SmartWattWattFunc.Policies;

namespace SmartWattWattFunc.Services;

public interface IEvChargeSyncService
{
    Task<RunSummary> RunAsync(CancellationToken cancellationToken = default);
}

public sealed class EvChargeSyncService(
    IOctopusGraphQlClient octopusClient,
    IFoxEssClient foxEssClient,
    IForceChargeScheduleBuilder scheduleBuilder,
    ITestModeLogger testModeLogger,
    ScheduleOptions options,
    TimeProvider timeProvider,
    ILogger<EvChargeSyncService> logger) : IEvChargeSyncService
{
    public async Task<RunSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var started = timeProvider.GetUtcNow();
        IReadOnlyList<EvDispatch> dispatches = [];
        string? octopusError = null;

        try
        {
            dispatches = await octopusClient.GetPlannedDispatchesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            octopusError = ex.Message;
            logger.LogError(ex, "Octopus GraphQL failed for run {RunId}; treating as no dispatch windows.", runId);
        }

        var desired = scheduleBuilder.Build(started, dispatches);
        var current = await foxEssClient.GetForceChargeScheduleAsync(cancellationToken);
        var shouldWrite = desired.RequiresWriteComparedTo(current);
        var writeApplied = false;

        if (options.TestMode)
        {
            if (shouldWrite)
            {
                var plan = testModeLogger.CreateSchedulePlan(runId, started, desired, current, dispatches);
                testModeLogger.LogSchedulePlan(plan);
            }
            else
            {
                var plan = testModeLogger.CreateNoChangePlan(runId, started, desired);
                testModeLogger.LogNoChange(plan);
            }
        }
        else if (!options.SyncEnabled)
        {
            logger.LogInformation("Sync disabled. Dry run for {RunId}. Desired mode {Mode}.", runId, desired.Mode);
        }
        else if (shouldWrite)
        {
            await foxEssClient.SetForceChargeScheduleAsync(desired, cancellationToken);
            writeApplied = true;
            logger.LogInformation(
                "Applied Fox ESS schedule for {RunId}. Mode {Mode}. Slot1 {Slot1Start}-{Slot1End}. Slot2 {Slot2Start}-{Slot2End}.",
                runId,
                desired.Mode,
                desired.Slot1.Start,
                desired.Slot1.End,
                desired.Slot2.Start,
                desired.Slot2.End);
        }
        else
        {
            logger.LogInformation("No Fox ESS write required for {RunId}. Desired mode {Mode}.", runId, desired.Mode);
        }

        return new RunSummary
        {
            RunId = runId,
            TimestampUtc = started,
            OctopusDispatchCount = dispatches.Count,
            OctopusError = octopusError,
            DesiredSchedule = desired,
            CurrentSchedule = current,
            WriteApplied = writeApplied,
            TestMode = options.TestMode,
            Success = true,
            DurationMs = (long)(timeProvider.GetUtcNow() - started).TotalMilliseconds
        };
    }
}

public sealed record RunSummary
{
    public required Guid RunId { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required int OctopusDispatchCount { get; init; }
    public string? OctopusError { get; init; }
    public required ForceChargeSchedule DesiredSchedule { get; init; }
    public required ForceChargeSchedule CurrentSchedule { get; init; }
    public required bool WriteApplied { get; init; }
    public required bool TestMode { get; init; }
    public required bool Success { get; init; }
    public required long DurationMs { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = false });
}
