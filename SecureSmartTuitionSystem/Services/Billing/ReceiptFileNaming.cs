using System.Globalization;
namespace SecureSmartTuitionSystem.Services.Billing;
public static class ReceiptFileNaming
{
    public static string ForRange(DateTime start, DateTime end)
    {
        start = new DateTime(start.Year, start.Month, 1);
        end = new DateTime(end.Year, end.Month, 1);
        if (end < start) throw new ArgumentOutOfRangeException(nameof(end));
        var first = start.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        var last = end.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        return start == end ? $"Receipt - {first} - PAID" : $"Receipt - {first} to {last} - PAID";
    }
}
