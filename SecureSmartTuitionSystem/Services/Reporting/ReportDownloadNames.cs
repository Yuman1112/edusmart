using System.Globalization;

namespace SecureSmartTuitionSystem.Services.Reporting;

public static class ReportDownloadNames
{
    public static string Period(DateTime start, DateTime end)
    {
        string MonthLabel(DateTime date) => date.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        return start.Year == end.Year && start.Month == end.Month
            ? MonthLabel(start)
            : $"{MonthLabel(start)} to {MonthLabel(end)}";
    }

    public static string FileName(string title, DateTime start, DateTime end, string extension) =>
        $"EduSmart - {title} - {Period(start, end)}.{extension}";
}
