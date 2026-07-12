using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Services;

public interface ITestModeLogger
{
    TestModeSchedulePlan CreateSchedulePlan(
        Guid runId,
        DateTimeOffset timestampUtc,
        ForceChargeSchedule desired,
        ForceChargeSchedule current,
        IReadOnlyList<EvDispatch> dispatches);

    TestModeNoChange CreateNoChangePlan(
        Guid runId,
        DateTimeOffset timestampUtc,
        ForceChargeSchedule desired);

    void LogSchedulePlan(TestModeSchedulePlan plan);

    void LogNoChange(TestModeNoChange plan);
}
