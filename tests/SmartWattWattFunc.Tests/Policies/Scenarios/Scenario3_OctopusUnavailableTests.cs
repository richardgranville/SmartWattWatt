using Moq;
using SmartWattWattFunc.Configuration;
using SmartWattWattFunc.Integrations.FoxEss;
using SmartWattWattFunc.Integrations.Octopus;
using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Tests.Policies.Scenarios;

public sealed class Scenario3_OctopusUnavailableTests
{
    [Fact]
    public async Task S3_T6_OctopusUnavailable_EnforcesDefault()
    {
        var octopus = new Mock<IOctopusGraphQlClient>();
        octopus.Setup(x => x.GetPlannedDispatchesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("auth failed"));

        var fox = new Mock<IFoxEssClient>();
        fox.Setup(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScheduleTestSupport.OvernightAdjustedSchedule());
        fox.Setup(x => x.SetForceChargeScheduleAsync(It.IsAny<ForceChargeSchedule>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var options = new ScheduleOptions { SyncEnabled = true };
        var service = ServiceTestSupport.CreateService(
            octopus.Object,
            fox.Object,
            options,
            ServiceTestSupport.CreateCaptureLogger());

        var summary = await service.RunAsync();

        Assert.True(summary.Success);
        Assert.NotNull(summary.OctopusError);
        Assert.Equal(ScheduleMode.Default, summary.DesiredSchedule!.Mode);
        fox.Verify(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()), Times.Once);
        fox.Verify(x => x.SetForceChargeScheduleAsync(
            It.Is<ForceChargeSchedule>(s => s.Mode == ScheduleMode.Default),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(summary.WriteApplied);
    }

    [Fact]
    public async Task S3_T7_OctopusUnavailable_AlreadyDefault_NoWrite()
    {
        var octopus = new Mock<IOctopusGraphQlClient>();
        octopus.Setup(x => x.GetPlannedDispatchesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("auth failed"));

        var fox = new Mock<IFoxEssClient>();
        fox.Setup(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScheduleTestSupport.DefaultSchedule());

        var options = new ScheduleOptions { SyncEnabled = true };
        var service = ServiceTestSupport.CreateService(
            octopus.Object,
            fox.Object,
            options,
            ServiceTestSupport.CreateCaptureLogger());

        var summary = await service.RunAsync();

        Assert.True(summary.Success);
        Assert.NotNull(summary.OctopusError);
        Assert.False(summary.WriteApplied);
        fox.Verify(x => x.GetForceChargeScheduleAsync(It.IsAny<CancellationToken>()), Times.Once);
        fox.Verify(x => x.SetForceChargeScheduleAsync(It.IsAny<ForceChargeSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
