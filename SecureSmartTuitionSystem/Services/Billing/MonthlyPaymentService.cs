using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using Stripe;
using Stripe.Checkout;
namespace SecureSmartTuitionSystem.Services.Billing;
public sealed class MonthlyPaymentService(ApplicationDbContext db, StripeClient stripe, IConfiguration config)
{
    private string BaseUrl()
    {
        var raw = config["App:PublicBaseUrl"] ?? "https://localhost:7086";
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("App:PublicBaseUrl must be an absolute HTTPS URL.");
        return raw.TrimEnd('/');
    }
    public async Task<string> CheckoutAsync(int invoiceId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var invoice = await db.Set<MonthlyInvoice>().SingleAsync(x => x.MonthlyInvoiceId == invoiceId, ct);
        if (invoice.Status != MonthlyInvoiceStatus.Pending || invoice.Total <= 0)
            throw new InvalidOperationException("This bill is no longer awaiting payment.");
        var sessions = new SessionService(stripe);
        if (invoice.CheckoutSessionId != null)
        {
            var previous = await sessions.GetAsync(invoice.CheckoutSessionId, cancellationToken: ct);
            if (previous.PaymentStatus == "paid") throw new InvalidOperationException("Payment is awaiting verification. Use Check payment status below.");
            if (previous.Status == "open" && !string.IsNullOrWhiteSpace(previous.Url))
            { await tx.CommitAsync(ct); return previous.Url; }
            if (previous.Status != "expired") throw new InvalidOperationException("The existing checkout needs verification before another payment can be opened.");
            invoice.CheckoutKey = Guid.NewGuid(); invoice.CheckoutSessionId = null;
        }
        var key = invoice.CheckoutKey.ToString("N");
        var session = await sessions.CreateAsync(new SessionCreateOptions {
            Mode = "payment", PaymentMethodTypes = new List<string> { "card" },
            ClientReferenceId = invoice.MonthlyInvoiceId.ToString(CultureInfo.InvariantCulture),
            Metadata = new Dictionary<string,string> { ["billing_kind"] = "monthly", ["invoice_id"] = invoice.MonthlyInvoiceId.ToString(CultureInfo.InvariantCulture), ["checkout_key"] = key },
            LineItems = new List<SessionLineItemOptions> { new() { Quantity = 1, PriceData = new SessionLineItemPriceDataOptions {
                Currency = "myr", UnitAmount = checked((long)decimal.Round(invoice.Total * 100m, 0)),
                ProductData = new SessionLineItemPriceDataProductDataOptions { Name = $"EduSmart {invoice.Number} - {invoice.Month:yyyy-MM}" } } } },
            SuccessUrl = BaseUrl() + $"/MonthlyBilling/Details/{invoice.MonthlyInvoiceId}?returned=true",
            CancelUrl = BaseUrl() + $"/MonthlyBilling/Details/{invoice.MonthlyInvoiceId}"
        }, new RequestOptions { IdempotencyKey = "edusmart-monthly-" + key }, ct);
        if (string.IsNullOrWhiteSpace(session.Url)) throw new InvalidOperationException("The checkout provider did not return a payment URL.");
        invoice.CheckoutSessionId = session.Id;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return session.Url;
    }
    public async Task VerifyAsync(int invoiceId, CancellationToken ct)
    {
        var invoice = await db.Set<MonthlyInvoice>().AsNoTracking().SingleAsync(x => x.MonthlyInvoiceId == invoiceId, ct);
        if (invoice.CheckoutSessionId == null) throw new InvalidOperationException("No checkout has been opened for this bill.");
        var session = await new SessionService(stripe).GetAsync(invoice.CheckoutSessionId, cancellationToken: ct);
        if (session.PaymentStatus != "paid") throw new InvalidOperationException("The provider has not confirmed payment yet.");
        await CompleteAsync(session, ct);
    }
    // Call only with a signature-verified webhook or a Session retrieved from Stripe.
    // Browser URL parameters never decide that an invoice is paid.
    public async Task CompleteAsync(Session session, CancellationToken ct)
    {
        if (session.Metadata == null || !session.Metadata.TryGetValue("billing_kind", out var kind) || kind != "monthly" ||
            !session.Metadata.TryGetValue("invoice_id", out var rawId) || !int.TryParse(rawId, out var id) ||
            !session.Metadata.TryGetValue("checkout_key", out var key)) throw new InvalidOperationException("Monthly checkout metadata is invalid.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var invoice = await db.Set<MonthlyInvoice>().SingleAsync(x => x.MonthlyInvoiceId == id, ct);
        if (session.PaymentStatus != "paid" || session.Status != "complete" || session.Currency != "myr" ||
            session.AmountTotal != checked((long)decimal.Round(invoice.Total * 100m, 0)) || invoice.Total <= 0 ||
            session.Id != invoice.CheckoutSessionId || key != invoice.CheckoutKey.ToString("N") || string.IsNullOrWhiteSpace(session.PaymentIntentId))
            throw new InvalidOperationException("Monthly payment verification failed.");
        if (invoice.Status == MonthlyInvoiceStatus.Paid)
        {
            if (invoice.PaymentIntentId != session.PaymentIntentId) throw new InvalidOperationException("A different payment is already recorded for this bill.");
            await tx.CommitAsync(ct); return;
        }
        if (invoice.Status != MonthlyInvoiceStatus.Pending) throw new InvalidOperationException("This invoice is no longer payable.");
        // Stripe stores the payment timestamp. Use it so retries do not change income month.
        var intent = await new PaymentIntentService(stripe).GetAsync(session.PaymentIntentId, cancellationToken: ct);
        if (intent.Status != "succeeded" || intent.Currency != "myr" || intent.AmountReceived != session.AmountTotal)
            throw new InvalidOperationException("The payment intent is not a matching successful payment.");
        if (string.IsNullOrWhiteSpace(intent.LatestChargeId)) throw new InvalidOperationException("The successful payment charge is missing.");
        var charge = await new ChargeService(stripe).GetAsync(intent.LatestChargeId, cancellationToken: ct);
        if (!charge.Paid || !charge.Captured || charge.Amount != session.AmountTotal || charge.Currency != "myr")
            throw new InvalidOperationException("The charge is not a matching captured payment.");
        invoice.Status = MonthlyInvoiceStatus.Paid;
        invoice.PaymentIntentId = session.PaymentIntentId;
        invoice.PaidAtUtc = charge.Created.ToUniversalTime();
        var guardians = await db.ParentStudents.Where(x => x.StudentProfileId == invoice.StudentProfileId && x.IsActive)
            .Select(x => x.ParentProfile.ApplicationUserId).ToListAsync(ct);
        var student = await db.StudentProfiles.Where(x => x.StudentProfileId == invoice.StudentProfileId).Select(x => x.ApplicationUserId).SingleAsync(ct);
        foreach (var userId in guardians.Concat(new[] { student }).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            db.UserNotifications.Add(new SecureSmartTuitionSystem.Models.Communication.UserNotification { UserId = userId!,
                Title = "Monthly tuition payment confirmed", Text = $"{invoice.Number}, {invoice.Month:yyyy-MM}: RM {invoice.Total:F2} received. The receipt is available in Monthly Tuition Bills." });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
