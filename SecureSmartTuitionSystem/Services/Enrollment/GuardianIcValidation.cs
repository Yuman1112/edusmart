using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;

namespace SecureSmartTuitionSystem.Services.Enrollment;

public static class GuardianIcValidation
{
    public const int MinimumAge = 18;
    public const int MaximumAge = 80;
    public const string DuplicateMessage =
        "This IC number is already registered. Please sign in with your existing parent account.";

    public static string? GetError(string? icNumber, DateOnly? onDate = null)
    {
        if (string.IsNullOrWhiteSpace(icNumber))
            return "Please enter the IC number.";
        if (!Regex.IsMatch(icNumber, "^[0-9]{6}-[0-9]{2}-[0-9]{4}$"))
            return "Please enter a valid IC number (000000-00-0000).";

        var today = onDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        int yy = int.Parse(icNumber[..2], CultureInfo.InvariantCulture);
        int year = today.Year / 100 * 100 + yy;
        if (yy > today.Year % 100)
            year -= 100;
        string dateText = $"{year:D4}{icNumber[2..6]}";
        if (!DateOnly.TryParseExact(dateText, "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var birthDate))
            return "The IC number contains an invalid date of birth.";
        if (birthDate >= today)
            return "The date of birth must be earlier than today.";

        int age = today.Year - birthDate.Year;
        if (today.Month < birthDate.Month || today.Month == birthDate.Month && today.Day < birthDate.Day)
            age--;
        return age < MinimumAge || age > MaximumAge
            ? "The IC number indicates an age outside the allowed range of 18–80."
            : null;
    }

    public static Task<bool> IsRegisteredAsync(
        ApplicationDbContext db, string icNumber, CancellationToken cancellationToken = default)
    {
        string digits = icNumber.Replace("-", "").Replace(" ", "");
        return db.ParentProfiles.AsNoTracking().AnyAsync(parent =>
            parent.IdentificationNumber != null
            && parent.IdentificationNumber.Replace("-", "").Replace(" ", "") == digits,
            cancellationToken);
    }
}
