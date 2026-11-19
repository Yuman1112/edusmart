using SecureSmartTuitionSystem.Models.Tutor;

namespace SecureSmartTuitionSystem.Services.Tutor;

public static class TutorAvailabilityPolicy
{
    public const int FirstSlot = 16; // 08:00
    public const int EndSlot = 44;   // 22:00, exclusive
    public const int MaxDailySlots = 16; // Eight hours, in half-hour slots.

    public static bool IsOpen(int value) => value >= 0 && value < 336
        && value / 48 is >= 1 and <= 6 && value % 48 >= FirstSlot && value % 48 < EndSlot;

    public static List<int> DaysOverLimit(IEnumerable<int> values) => values.Where(IsOpen).Distinct()
        .GroupBy(value => value / 48).Where(day => day.Count() > MaxDailySlots)
        .Select(day => day.Key).Order().ToList();

    public static string Version(TutorEmploymentType type, IEnumerable<int> slots) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(((int)type).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + string.Join(",", slots.Distinct().Order()))));

    public static bool IsTypeValid(TutorEmploymentType value) => Enum.IsDefined(value);
}
