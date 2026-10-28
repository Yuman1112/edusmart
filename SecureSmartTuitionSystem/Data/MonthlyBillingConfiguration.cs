using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Billing;
namespace SecureSmartTuitionSystem.Data;
public static class MonthlyBillingConfiguration
{
    public static void ConfigureMonthlyBilling(this ModelBuilder builder)
    {
        builder.Entity<MonthlyInvoice>(e => {
            e.ToTable("MonthlyInvoices"); e.HasIndex(x => new { x.StudentProfileId, x.Month }).IsUnique();
            e.HasIndex(x => x.CheckoutSessionId).IsUnique().HasFilter("[CheckoutSessionId] IS NOT NULL");
            e.HasIndex(x => x.PaymentIntentId).IsUnique().HasFilter("[PaymentIntentId] IS NOT NULL");
            e.Property(x => x.Month).HasColumnType("date");
            e.HasOne(x => x.StudentProfile).WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<MonthlyInvoiceLine>(e => {
            e.ToTable("MonthlyInvoiceLines"); e.HasIndex(x => new { x.MonthlyInvoiceId, x.StudentEnrollmentId }).IsUnique();
            e.HasOne(x => x.MonthlyInvoice).WithMany(x => x.Lines).HasForeignKey(x => x.MonthlyInvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<BillingReminder>(e => {
            e.ToTable("BillingReminders"); e.Property(x => x.Day).HasColumnType("date");
            e.HasIndex(x => new { x.MonthlyInvoiceId, x.UserId, x.Kind, x.Day }).IsUnique();
            e.HasOne(x => x.MonthlyInvoice).WithMany().HasForeignKey(x => x.MonthlyInvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
