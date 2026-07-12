using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SmartWattWattFunc.Services;

namespace SmartWattWattFunc.Functions;

public sealed class EvChargeSyncTimer(IEvChargeSyncService syncService, ILogger<EvChargeSyncTimer> logger)
{
    [Function(nameof(EvChargeSyncTimer))]
    public async Task Run([TimerTrigger("0 */15 * * * *", RunOnStartup = false)] TimerInfo timerInfo)
    {
        var summary = await syncService.RunAsync();
        logger.LogInformation("RunSummary {Summary}", summary.ToJson());
    }
}
