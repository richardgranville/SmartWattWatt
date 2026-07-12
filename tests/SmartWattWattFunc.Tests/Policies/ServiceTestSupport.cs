using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartWattWattFunc.Configuration;
using SmartWattWattFunc.Integrations.FoxEss;
using SmartWattWattFunc.Integrations.Octopus;
using SmartWattWattFunc.Models;
using SmartWattWattFunc.Policies;
using SmartWattWattFunc.Services;

namespace SmartWattWattFunc.Tests.Policies;

internal sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal sealed class CaptureTestModeLogger : ITestModeLogger
{
    private readonly TestModeLogger _inner;

    public CaptureTestModeLogger(
        FoxEssOptions foxEssOptions,
        ScheduleOptions scheduleOptions,
        TimeProvider? timeProvider = null)
    {
        _inner = new TestModeLogger(
            foxEssOptions,
            scheduleOptions,
            timeProvider ?? TimeProvider.System,
            NullLogger<TestModeLogger>.Instance);
    }

    public TestModeSchedulePlan? LastSchedulePlan { get; private set; }

    public TestModeNoChange? LastNoChange { get; private set; }

    public string? LastLoggedJson { get; private set; }

    public TestModeSchedulePlan CreateSchedulePlan(
        Guid runId,
        DateTimeOffset timestampUtc,
        ForceChargeSchedule desired,
        ForceChargeSchedule current,
        IReadOnlyList<EvDispatch> dispatches) =>
        _inner.CreateSchedulePlan(runId, timestampUtc, desired, current, dispatches);

    public TestModeNoChange CreateNoChangePlan(
        Guid runId,
        DateTimeOffset timestampUtc,
        ForceChargeSchedule desired) =>
        _inner.CreateNoChangePlan(runId, timestampUtc, desired);

    public void LogSchedulePlan(TestModeSchedulePlan plan)
    {
        LastSchedulePlan = plan;
        LastNoChange = null;
        LastLoggedJson = JsonSerializer.Serialize(plan);
    }

    public void LogNoChange(TestModeNoChange plan)
    {
        LastNoChange = plan;
        LastSchedulePlan = null;
        LastLoggedJson = JsonSerializer.Serialize(plan);
    }
}

internal static class ServiceTestSupport
{
    public static readonly FoxEssOptions DefaultFoxEssOptions = new()
    {
        DeviceSerialNumber = "H3-TEST-SERIAL",
        ApiToken = "secret-fox-token",
        TimeZoneId = "Europe/London"
    };

    public static EvChargeSyncService CreateService(
        IOctopusGraphQlClient octopus,
        IFoxEssClient fox,
        ScheduleOptions options,
        ITestModeLogger testModeLogger,
        DateTimeOffset? now = null,
        IForceChargeScheduleBuilder? builder = null) =>
        new(
            octopus,
            fox,
            builder ?? ScheduleTestSupport.CreateBuilder(),
            testModeLogger,
            options,
            now is null ? TimeProvider.System : new FakeTimeProvider(now.Value),
            NullLogger<EvChargeSyncService>.Instance);

    public static CaptureTestModeLogger CreateCaptureLogger(ScheduleOptions? options = null) =>
        new(DefaultFoxEssOptions, options ?? ScheduleTestSupport.Options);
}
