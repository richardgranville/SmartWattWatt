using Moq;
using SmartWattWattFunc.Configuration;
using SmartWattWattFunc.Integrations.FoxEss;
using SmartWattWattFunc.Integrations.Octopus;
using SmartWattWattFunc.Models;
using SmartWattWattFunc.Services;

namespace SmartWattWattFunc.Tests.Policies.Scenarios;

public sealed class Scenario6_TestModeTests
{
    private static readonly EvDispatch D1 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 3, 0, 5, 30);
    private static readonly EvDispatch D2 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 7, 30, 8, 30);
    private static readonly EvDispatch[] Dispatches = [D1, D2];

    private static readonly EvDispatch DayD1 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 14, 0, 15, 0);
    private static readonly EvDispatch DayD2 = ScheduleTestSupport.DispatchLocal(2026, 7, 13, 18, 0, 19, 0);
    private static readonly EvDispatch[] DaytimeDispatches = [DayD1, DayD2];

    [Fact]
    public async Task S6_T1_TestMode_WouldWrite_NoFoxSetCall()
    {
        var octopus = new Mock<IOctopusGraphQlClient>();
        octopus.Setup(x => x.GetPlannedDispatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dispatches);

        var fox = new Mock<IFoxEssClient>();
        fox.Setup(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScheduleTestSupport.DefaultSchedule());

        var capture = ServiceTestSupport.CreateCaptureLogger();
        var options = new ScheduleOptions { SyncEnabled = true, TestMode = true };
        var service = ServiceTestSupport.CreateService(
            octopus.Object,
            fox.Object,
            options,
            capture,
            ScheduleTestSupport.AtLocal(2026, 7, 13, 0, 15));

        var summary = await service.RunAsync();

        Assert.True(summary.Success);
        Assert.False(summary.WriteApplied);
        Assert.True(summary.TestMode);
        Assert.NotNull(capture.LastSchedulePlan);
        Assert.Null(capture.LastNoChange);

        var plan = capture.LastSchedulePlan!;
        Assert.Equal(ScheduleMode.OvernightAdjusted, plan.ScheduleMode);
        Assert.Contains(plan.SlotChanges, c => c.Slot == 1 && c.Action == "Update");
        Assert.Contains(plan.SlotChanges, c => c.Slot == 2 && c.Action == "Unchanged");

        var setCall = plan.ApiCalls.Single(c => c.Operation == "SetForceChargeSchedule");
        Assert.False(setCall.Executed);
        Assert.Equal(7, setCall.RequestBody!.StartTime1.Hour);
        Assert.Equal(30, setCall.RequestBody.StartTime1.Minute);
        Assert.Equal("[REDACTED]", setCall.Headers!["token"]);
        Assert.Equal("[REDACTED]", setCall.Headers["signature"]);
        Assert.DoesNotContain("secret-fox-token", capture.LastLoggedJson!, StringComparison.Ordinal);

        fox.Verify(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()), Times.Once);
        fox.Verify(x => x.SetForceChargeScheduleAsync(It.IsAny<ForceChargeSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task S6_T2_TestMode_AlreadyMatches_NoChangeLog()
    {
        var octopus = new Mock<IOctopusGraphQlClient>();
        octopus.Setup(x => x.GetPlannedDispatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dispatches);

        var fox = new Mock<IFoxEssClient>();
        fox.Setup(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScheduleTestSupport.OvernightAdjustedSchedule());

        var capture = ServiceTestSupport.CreateCaptureLogger();
        var options = new ScheduleOptions { TestMode = true };
        var service = ServiceTestSupport.CreateService(
            octopus.Object,
            fox.Object,
            options,
            capture,
            ScheduleTestSupport.AtLocal(2026, 7, 13, 0, 15));

        var summary = await service.RunAsync();

        Assert.False(summary.WriteApplied);
        Assert.NotNull(capture.LastNoChange);
        Assert.Null(capture.LastSchedulePlan);
        Assert.Empty(capture.LastNoChange!.SlotChanges);
        Assert.Single(capture.LastNoChange.ApiCalls);
        Assert.Equal("GetForceChargeSchedule", capture.LastNoChange.ApiCalls[0].Operation);
        Assert.True(capture.LastNoChange.ApiCalls[0].Executed);

        fox.Verify(x => x.SetForceChargeScheduleAsync(It.IsAny<ForceChargeSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task S6_T3_TestMode_DefaultRestoration()
    {
        var octopus = new Mock<IOctopusGraphQlClient>();
        octopus.Setup(x => x.GetPlannedDispatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EvDispatch>());

        var fox = new Mock<IFoxEssClient>();
        fox.Setup(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScheduleTestSupport.OvernightAdjustedSchedule());

        var capture = ServiceTestSupport.CreateCaptureLogger();
        var options = new ScheduleOptions { TestMode = true };
        var service = ServiceTestSupport.CreateService(
            octopus.Object,
            fox.Object,
            options,
            capture,
            ScheduleTestSupport.AtLocal(2026, 7, 13, 10, 0));

        var summary = await service.RunAsync();

        Assert.False(summary.WriteApplied);
        Assert.NotNull(capture.LastSchedulePlan);
        Assert.Equal(ScheduleMode.Default, capture.LastSchedulePlan!.ScheduleMode);
        Assert.Contains(capture.LastSchedulePlan.SlotChanges, c => c.Slot == 1 && c.Action == "Update" && c.Desired.Start == "23:30");

        var setCall = capture.LastSchedulePlan.ApiCalls.Single(c => c.Operation == "SetForceChargeSchedule");
        Assert.Equal(23, setCall.RequestBody!.StartTime1.Hour);
        Assert.Equal(30, setCall.RequestBody.StartTime1.Minute);

        fox.Verify(x => x.SetForceChargeScheduleAsync(It.IsAny<ForceChargeSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task S6_T4_TestMode_WithSyncDisabled_StillVerbose()
    {
        var octopus = new Mock<IOctopusGraphQlClient>();
        octopus.Setup(x => x.GetPlannedDispatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dispatches);

        var fox = new Mock<IFoxEssClient>();
        fox.Setup(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScheduleTestSupport.DefaultSchedule());

        var capture = ServiceTestSupport.CreateCaptureLogger();
        var options = new ScheduleOptions { SyncEnabled = false, TestMode = true };
        var service = ServiceTestSupport.CreateService(
            octopus.Object,
            fox.Object,
            options,
            capture,
            ScheduleTestSupport.AtLocal(2026, 7, 13, 0, 15));

        var summary = await service.RunAsync();

        Assert.False(summary.WriteApplied);
        Assert.NotNull(capture.LastSchedulePlan);
        fox.Verify(x => x.SetForceChargeScheduleAsync(It.IsAny<ForceChargeSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task S6_T5_TestMode_PreScheduledDaytimeWriteSuppressed()
    {
        var octopus = new Mock<IOctopusGraphQlClient>();
        octopus.Setup(x => x.GetPlannedDispatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaytimeDispatches);

        var fox = new Mock<IFoxEssClient>();
        fox.Setup(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScheduleTestSupport.DefaultSchedule());

        var capture = ServiceTestSupport.CreateCaptureLogger();
        var options = new ScheduleOptions { TestMode = true };
        var service = ServiceTestSupport.CreateService(
            octopus.Object,
            fox.Object,
            options,
            capture,
            ScheduleTestSupport.AtLocal(2026, 7, 13, 10, 0));

        var summary = await service.RunAsync();

        Assert.False(summary.WriteApplied);
        Assert.NotNull(capture.LastSchedulePlan);
        Assert.Equal(ScheduleMode.PreScheduled, capture.LastSchedulePlan!.ScheduleMode);
        Assert.All(capture.LastSchedulePlan.SlotChanges, c => Assert.Equal("Update", c.Action));

        var setCall = capture.LastSchedulePlan.ApiCalls.Single(c => c.Operation == "SetForceChargeSchedule");
        Assert.Equal(14, setCall.RequestBody!.StartTime1.Hour);
        Assert.Equal(18, setCall.RequestBody.StartTime2.Hour);

        fox.Verify(x => x.SetForceChargeScheduleAsync(It.IsAny<ForceChargeSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task S6_T6_TestMode_FoxGetAlwaysCalled()
    {
        var octopus = new Mock<IOctopusGraphQlClient>();
        octopus.Setup(x => x.GetPlannedDispatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dispatches);

        var fox = new Mock<IFoxEssClient>();
        fox.Setup(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScheduleTestSupport.DefaultSchedule());

        var capture = ServiceTestSupport.CreateCaptureLogger();
        var options = new ScheduleOptions { SyncEnabled = true, TestMode = true };
        var service = ServiceTestSupport.CreateService(
            octopus.Object,
            fox.Object,
            options,
            capture,
            ScheduleTestSupport.AtLocal(2026, 7, 13, 0, 15));

        await service.RunAsync();

        fox.Verify(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()), Times.Once);
        fox.Verify(x => x.SetForceChargeScheduleAsync(It.IsAny<ForceChargeSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task S6_T7_ProductionMode_WriteStillOccurs()
    {
        var octopus = new Mock<IOctopusGraphQlClient>();
        octopus.Setup(x => x.GetPlannedDispatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dispatches);

        var fox = new Mock<IFoxEssClient>();
        fox.Setup(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScheduleTestSupport.DefaultSchedule());
        fox.Setup(x => x.SetForceChargeScheduleAsync(It.IsAny<ForceChargeSchedule>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var capture = ServiceTestSupport.CreateCaptureLogger();
        var options = new ScheduleOptions { SyncEnabled = true, TestMode = false };
        var service = ServiceTestSupport.CreateService(
            octopus.Object,
            fox.Object,
            options,
            capture,
            ScheduleTestSupport.AtLocal(2026, 7, 13, 0, 15));

        var summary = await service.RunAsync();

        Assert.True(summary.WriteApplied);
        Assert.False(summary.TestMode);
        Assert.Null(capture.LastSchedulePlan);
        Assert.Null(capture.LastNoChange);

        fox.Verify(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()), Times.Once);
        fox.Verify(x => x.SetForceChargeScheduleAsync(
            It.Is<ForceChargeSchedule>(s => s.Mode == ScheduleMode.OvernightAdjusted),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
