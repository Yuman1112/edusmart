using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.Services.Reporting;

public sealed record IncomeMonth(DateTime Month, decimal Gross, decimal Refunds, int PaymentCount)
{
    public decimal Net => Gross - Refunds;
}

public sealed record IncomeReport(DateTime EndMonth, IReadOnlyList<IncomeMonth> Months)
{
    public decimal Gross => Months.Sum(x => x.Gross);
    public decimal Refunds => Months.Sum(x => x.Refunds);
    public decimal Net => Gross - Refunds;
}

public sealed class IncomeReportService(ApplicationDbContext db)
{
    public static bool TryEndMonth(string? text, DateTime nowUtc, out DateTime endMonth)
    {
        DateTime today = nowUtc.AddHours(8);
        DateTime current = new(today.Year, today.Month, 1);
        if (string.IsNullOrWhiteSpace(text)) { endMonth = current; return true; }
        if (!DateTime.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out endMonth)) return false;
        endMonth = new DateTime(endMonth.Year, endMonth.Month, 1);
        return endMonth >= new DateTime(2000, 1, 1) && endMonth <= current;
    }

    public async Task<IncomeReport> BuildAsync(DateTime endMonth, int months, CancellationToken ct = default)
    {
        if (months < 1 || months > 12) throw new ArgumentOutOfRangeException(nameof(months));
        endMonth = new DateTime(endMonth.Year, endMonth.Month, 1);
        DateTime start = endMonth.AddMonths(1 - months);
        DateTime startUtc = start.AddHours(-8), endUtc = endMonth.AddMonths(1).AddHours(-8);

        // Dates are stored as UTC; classify months in Malaysia time (UTC+8).
        // Group in SQL so the report does not load all payment rows.
        var payments = await db.Payments.AsNoTracking()
            .Where(p => (p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Refunded) &&
                p.Currency == "MYR" && p.CompletedAt >= startUtc && p.CompletedAt < endUtc)
            .GroupBy(p => new { Year = p.CompletedAt!.Value.AddHours(8).Year, Month = p.CompletedAt!.Value.AddHours(8).Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Gross = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync(ct);
        var recurring = await db.Set<MonthlyInvoice>().AsNoTracking()
            .Where(p => p.Status == MonthlyInvoiceStatus.Paid && p.Total > 0 && p.PaidAtUtc >= startUtc && p.PaidAtUtc < endUtc)
            .GroupBy(p => new { Year = p.PaidAtUtc!.Value.AddHours(8).Year, Month = p.PaidAtUtc!.Value.AddHours(8).Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Gross = g.Sum(x => x.Total), Count = g.Count() }).ToListAsync(ct);
        var refunds = await db.EnrollmentExitRequests.AsNoTracking()
            .Where(r => r.RefundStatus == ExitRefundStatus.Completed && r.PaymentId != null &&
                r.Payment!.Currency == "MYR" && r.RefundCompletedAtUtc >= startUtc && r.RefundCompletedAtUtc < endUtc)
            .GroupBy(r => new { Year = r.RefundCompletedAtUtc!.Value.AddHours(8).Year, Month = r.RefundCompletedAtUtc!.Value.AddHours(8).Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.ApprovedRefund) })
            .ToListAsync(ct);

        var rows = Enumerable.Range(0, months).Select(i =>
        {
            var month = start.AddMonths(i);
            var payment = payments.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month);
            var refund = refunds.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month);
            var monthly = recurring.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month);
            return new IncomeMonth(month, (payment?.Gross ?? 0m) + (monthly?.Gross ?? 0m), refund?.Amount ?? 0m, (payment?.Count ?? 0) + (monthly?.Count ?? 0));
        }).ToArray();
        return new IncomeReport(endMonth, rows);
    }
}
