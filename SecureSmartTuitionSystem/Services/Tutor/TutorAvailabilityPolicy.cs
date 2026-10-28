using SecureSmartTuitionSystem.Models.Tutor;
namespace SecureSmartTuitionSystem.Services.Tutor;
public static class TutorAvailabilityPolicy
{
    public const int FirstSlot = 16; // 08:00
    public const int EndSlot = 44;   // 22:00, exclusive
    public static bool IsOpen(int value) => value >= 0 && value < 336
        && value / 48 is >= 1 and <= 6 && value % 48 >= FirstSlot && value % 48 < EndSlot;
    public static bool IsBreak(int slot) => slot is 24 or 25 or 36 or 37;
    public static List<int> FullTimeSlots() => Enumerable.Range(1, 6)
        .SelectMany(day => Enumerable.Range(FirstSlot, EndSlot - FirstSlot)
            .Where(slot => !IsBreak(slot)).Select(slot => day * 48 + slot)).ToList();
    public static bool IsTypeValid(TutorEmploymentType value) => Enum.IsDefined(value);
}
