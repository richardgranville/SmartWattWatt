using SmartWattWattFunc.Configuration;
using SmartWattWattFunc.Models;

namespace SmartWattWattFunc.Policies;

public interface IForceChargeScheduleBuilder
{
    ForceChargeSchedule Build(DateTimeOffset nowUtc, IReadOnlyList<EvDispatch> plannedDispatches);
}

public sealed class ForceChargeScheduleBuilder(ScheduleOptions options) : IForceChargeScheduleBuilder
{
    private static readonly TimeSpan Noon = TimeSpan.FromHours(12);
    private static readonly TimeSpan MorningCutoff = TimeSpan.FromHours(9);

    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
    private readonly TimeSpan _daytimeGapStart = options.DefaultSlot2End.ToTimeSpan();
    private readonly TimeSpan _daytimeGapEnd = options.DefaultSlot1Start.ToTimeSpan();
    private readonly TimeSpan _defaultFcEveningStart = options.DefaultSlot1Start.ToTimeSpan();

    public ForceChargeSchedule Build(DateTimeOffset nowUtc, IReadOnlyList<EvDispatch> plannedDispatches)
    {
        if (plannedDispatches is null || plannedDispatches.Count == 0)
        {
            return CreateDefault();
        }

        var now = ToLocal(nowUtc);
        var pending = plannedDispatches
            .Where(d => d.IsPendingAt(now))
            .OrderBy(d => d.Start)
            .ToList();

        if (pending.Count == 0)
        {
            return CreateDefault();
        }

        var outsidePending = pending.Where(IsOutsideDefault).ToList();
        var insidePending = pending.Where(d => !IsOutsideDefault(d)).ToList();

        if (outsidePending.Count == 0)
        {
            return CreateDefault();
        }

        if (IsPreMidnightEvening(now, pending))
        {
            return CreateDefault();
        }

        if (ShouldUseProgressiveStaging(now, outsidePending, insidePending))
        {
            return BuildProgressiveStaging(now, insidePending, outsidePending);
        }

        if (IsDaytimeGap(now) && !InActiveDispatch(now, pending))
        {
            var afternoonPreSchedule = TryCreateAfternoonPreScheduled(now, plannedDispatches);
            if (afternoonPreSchedule is not null)
            {
                return afternoonPreSchedule;
            }

            if (pending.Count >= 2)
            {
                return CreatePreScheduled(pending);
            }

            return BuildSingleOutsideDaytime(outsidePending[0]);
        }

        if (InDefaultForceCharge(now) && outsidePending.Count == 1)
        {
            return CreateOvernightAdjusted(outsidePending[0]);
        }

        if (outsidePending.Count == 1)
        {
            return BuildSingleOutsideDaytime(outsidePending[0]);
        }

        return CreateDefault();
    }

    private ForceChargeSchedule BuildProgressiveStaging(
        DateTimeOffset now,
        IReadOnlyList<EvDispatch> insidePending,
        IReadOnlyList<EvDispatch> outsidePending)
    {
        var outside = outsidePending.OrderBy(d => d.Start).ToList();
        var defaultSlot2 = CreateDefaultSlot2();

        if (InDefaultForceCharge(now))
        {
            if (InActiveInsideDefaultDispatch(now, insidePending))
            {
                return CreateProgressive(outside[0], defaultSlot2);
            }

            if (now.TimeOfDay >= TimeSpan.FromHours(4) &&
                now.TimeOfDay < _daytimeGapStart &&
                outside.Count >= 2)
            {
                return CreateProgressive(outside[0], outside[1]);
            }

            return CreateDefault();
        }

        if (IsDaytimeGap(now))
        {
            if (outside.Count == 0)
            {
                return CreateDefault();
            }

            if (now.TimeOfDay >= TimeSpan.FromHours(6) &&
                now.TimeOfDay < TimeSpan.FromHours(11).Add(TimeSpan.FromMinutes(45)) &&
                outside.Count >= 2)
            {
                if (now.TimeOfDay >= TimeSpan.FromHours(8))
                {
                    return CreateProgressive(outside[^1], defaultSlot2);
                }

                return CreateProgressive(outside[^1], outside[^2]);
            }
        }

        if (outside.Count == 1)
        {
            return BuildSingleOutsideDaytime(outside[0]);
        }

        return CreateDefault();
    }

    private bool ShouldUseProgressiveStaging(
        DateTimeOffset now,
        IReadOnlyList<EvDispatch> outsidePending,
        IReadOnlyList<EvDispatch> insidePending)
    {
        if (outsidePending.Count < 2)
        {
            return false;
        }

        if (insidePending.Count >= 1)
        {
            return true;
        }

        if (InDefaultForceCharge(now))
        {
            return true;
        }

        if (!IsDaytimeGap(now))
        {
            return false;
        }

        var earliestOutsideStart = outsidePending.Min(d => ToLocal(d.Start).TimeOfDay);
        return earliestOutsideStart < Noon;
    }

    private ForceChargeSchedule? TryCreateAfternoonPreScheduled(
        DateTimeOffset now,
        IReadOnlyList<EvDispatch> plannedDispatches)
    {
        var afternoonOutside = plannedDispatches
            .Where(IsOutsideDefault)
            .Where(d => ToLocal(d.Start).Date == now.Date && ToLocal(d.Start).TimeOfDay >= Noon)
            .OrderBy(d => d.Start)
            .ToList();

        if (afternoonOutside.Count < 2)
        {
            return null;
        }

        return CreatePreScheduled(afternoonOutside.Take(2).ToList());
    }

    private ForceChargeSchedule BuildSingleOutsideDaytime(EvDispatch dispatch)
    {
        var startLocal = ToLocal(dispatch.Start);
        var endLocal = ToLocal(dispatch.End);

        if (startLocal.TimeOfDay >= Noon)
        {
            return CreatePreScheduled([dispatch]);
        }

        if (endLocal.TimeOfDay > MorningCutoff)
        {
            return CreateProgressive(dispatch, CreateDefaultSlot2());
        }

        return CreateOvernightAdjusted(dispatch);
    }

    private ForceChargeSchedule CreateDefault() =>
        new(ScheduleMode.Default, CreateDefaultSlot1(), CreateDefaultSlot2());

    private ForceChargeSchedule CreateOvernightAdjusted(EvDispatch outside) =>
        new(ScheduleMode.OvernightAdjusted, TimeSlot.FromDispatch(outside.Start, outside.End, _timeZone), CreateDefaultSlot2());

    private ForceChargeSchedule CreatePreScheduled(IReadOnlyList<EvDispatch> pending)
    {
        var slot1 = TimeSlot.FromDispatch(pending[0].Start, pending[0].End, _timeZone);
        var slot2 = pending.Count >= 2
            ? TimeSlot.FromDispatch(pending[1].Start, pending[1].End, _timeZone)
            : CreateDefaultSlot2();
        return new ForceChargeSchedule(ScheduleMode.PreScheduled, slot1, slot2);
    }

    private ForceChargeSchedule CreateProgressive(EvDispatch slot1Dispatch, TimeSlot slot2) =>
        new(ScheduleMode.ProgressiveStaging, TimeSlot.FromDispatch(slot1Dispatch.Start, slot1Dispatch.End, _timeZone), slot2);

    private ForceChargeSchedule CreateProgressive(EvDispatch slot1Dispatch, EvDispatch slot2Dispatch) =>
        CreateProgressive(slot1Dispatch, TimeSlot.FromDispatch(slot2Dispatch.Start, slot2Dispatch.End, _timeZone));

    private TimeSlot CreateDefaultSlot1() =>
        new(true, options.DefaultSlot1Start, options.DefaultSlot1End);

    private TimeSlot CreateDefaultSlot2() =>
        new(true, options.DefaultSlot2Start, options.DefaultSlot2End);

    private bool IsOutsideDefault(EvDispatch dispatch)
    {
        var endLocal = ToLocal(dispatch.End);
        return endLocal.TimeOfDay > _daytimeGapStart;
    }

    private bool IsDaytimeGap(DateTimeOffset nowLocal) =>
        nowLocal.TimeOfDay >= _daytimeGapStart && nowLocal.TimeOfDay < _daytimeGapEnd;

    private bool InDefaultForceCharge(DateTimeOffset nowLocal) =>
        nowLocal.TimeOfDay >= _defaultFcEveningStart || nowLocal.TimeOfDay < _daytimeGapStart;

    private bool IsPreMidnightEvening(DateTimeOffset nowLocal, IReadOnlyList<EvDispatch> pending) =>
        ToLocal(pending[0].Start).Date > nowLocal.Date;

    private static bool InActiveDispatch(DateTimeOffset nowLocal, IReadOnlyList<EvDispatch> pending) =>
        pending.Any(d => d.IsActiveAt(nowLocal));

    private static bool InActiveInsideDefaultDispatch(DateTimeOffset nowLocal, IReadOnlyList<EvDispatch> insidePending) =>
        insidePending.Any(d => d.IsActiveAt(nowLocal));

    private DateTimeOffset ToLocal(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, _timeZone);
}
