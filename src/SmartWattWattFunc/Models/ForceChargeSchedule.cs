namespace SmartWattWattFunc.Models;

public sealed record ForceChargeSchedule(ScheduleMode Mode, TimeSlot Slot1, TimeSlot Slot2)
{
    public bool Matches(ForceChargeSchedule other) =>
        Slot1.Matches(other.Slot1) &&
        Slot2.Matches(other.Slot2);

    public bool RequiresWriteComparedTo(ForceChargeSchedule? current) =>
        current is null || !Matches(current);
}
