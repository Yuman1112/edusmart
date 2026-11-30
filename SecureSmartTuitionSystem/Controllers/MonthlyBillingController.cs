using SecureSmartTuitionSystem.Services.Tables;
using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Services.Billing;
using SecureSmartTuitionSystem.ViewModels.Billing;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles="Admin,Parent,Student")]
[ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
public sealed class MonthlyBillingController(MonthlyBillingService billing, MonthlyPaymentService payments,
    ILogger<MonthlyBillingController> logger, IDataProtectionProvider protection) : Controller
{
    private IDataProtector RecoveryProtection => protection.CreateProtector("EduSmart.MonthlyBillRecovery.v1");
    [Authorize(Roles = "Admin"), HttpGet]
    public async Task<IActionResult> Recovery(string? month, CancellationToken ct)
    {
        var selected = MonthlyBillingService.MonthOf(MonthlyBillingService.MalaysiaToday).AddMonths(-1);
        if (!string.IsNullOrWhiteSpace(month) && (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out selected) ||
            selected.Year < 2000 || selected > MonthlyBillingService.MonthOf(MonthlyBillingService.MalaysiaToday))) return BadRequest("Choose a valid billing month.");
        var rows = await billing.MissingAsync(selected, ct);
        var approval = new MonthlyRecoveryApproval(User.FindFirstValue(ClaimTypes.NameIdentifier)!, selected, DateTime.UtcNow.AddMinutes(15), rows.ToDictionary(x => x.StudentId, x => x.Fingerprint));
        return View(new MonthlyRecoveryView(selected, rows, RecoveryProtection.Protect(JsonSerializer.Serialize(approval))));
    }
    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Recover(string token, bool confirmed, CancellationToken ct)
    {
        MonthlyRecoveryApproval? approval;
        try { approval = JsonSerializer.Deserialize<MonthlyRecoveryApproval>(RecoveryProtection.Unprotect(token ?? "")); }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException) { return BadRequest("The preview expired. Open a new recovery preview."); }
        if (!confirmed || approval == null || approval.ExpiresUtc < DateTime.UtcNow || approval.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return BadRequest("Confirm a valid recovery preview.");
        try {
            var generated = await billing.GenerateMonthAsync(approval.Month, approval.Bills, ct);
            TempData["SuccessMessage"] = $"Created {generated} missing bill(s). Existing or concurrently created bills were skipped. Each new bill has a fresh payment period.";
        }
        catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message + " Some earlier records may already have been created; they will not be duplicated."; }
        return RedirectToAction(nameof(Recovery), new { month = approval.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture) });
    }
    [HttpGet]
    public async Task<IActionResult> Index(string? month, string? status, int page = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || status is not (null or "" or "pending" or "paid" or "overdue")) return BadRequest("Choose a valid bill status.");
        var allowed = billing.Accessible(User).AsNoTracking();
        var now = DateTime.UtcNow;
        decimal outstanding = await allowed.Where(x => x.Status == MonthlyInvoiceStatus.Pending).SumAsync(x => x.Total, ct);
        var query = allowed;
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var selected) || selected.Year < 2000 || selected > MonthlyBillingService.MonthOf(MonthlyBillingService.MalaysiaToday)) return BadRequest("Choose a valid month.");
            query = query.Where(x => x.Month == selected);
        }
        if (status == "paid") query = query.Where(x => x.Status == MonthlyInvoiceStatus.Paid);
        if (status == "pending") query = query.Where(x => x.Status == MonthlyInvoiceStatus.Pending);
        if (status == "overdue") query = query.Where(x => x.Status == MonthlyInvoiceStatus.Pending && x.DueAtUtc < now);
        int count = await query.CountAsync(ct), pages = Math.Max(1, (int)Math.Ceiling(count / 25d)); page = Math.Clamp(page, 1, pages);
        var rows = await TableOrder.Apply(query.Include(x => x.StudentProfile), HttpContext?.Request.Query, "month", true,
            ("bill", x => x.MonthlyInvoiceId), ("student", x => x.StudentProfile.FullName), ("month", x => x.Month),
            ("amount", x => x.Total), ("due", x => x.DueAtUtc),
            ("status", x => x.Status == MonthlyInvoiceStatus.Pending && x.DueAtUtc < now ? "Overdue" : x.Status == MonthlyInvoiceStatus.Pending ? "Pending" : x.Status == MonthlyInvoiceStatus.Paid ? "Paid" : "Cancelled"))
            .ThenByDescending(x => x.MonthlyInvoiceId).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        return View(new MonthlyBillingIndexViewModel { Rows=rows, Page=page, TotalPages=pages, TotalRows=count, Month=month, Status=status, Outstanding=outstanding });
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id, bool returned = false, CancellationToken ct = default)
    {
        var invoice = await billing.Accessible(User).AsNoTracking().Include(x => x.StudentProfile).Include(x => x.Lines).SingleOrDefaultAsync(x => x.MonthlyInvoiceId == id, ct);
        if (invoice == null) return NotFound();
        ViewBag.Returned = returned;
        if (User.IsInRole("Admin")) ViewBag.EmailStatus = await billing.EmailStatusAsync(id, ct);
        return View(invoice);
    }
    [HttpGet]
    public async Task<IActionResult> Receipt(int id, CancellationToken ct = default)
    {
        var bill = await billing.Accessible(User).AsNoTracking().Include(x => x.StudentProfile).Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.MonthlyInvoiceId == id && x.Status == MonthlyInvoiceStatus.Paid && x.PaidAtUtc != null, ct);
        if (bill == null || !bill.PaidAtUtc.HasValue) return NotFound();
        return View("~/Views/Payments/Receipt.cshtml", new PaymentReceiptViewModel {
            DownloadFileName = ReceiptFileNaming.ForRange(bill.Month, bill.Month),
            PaymentNumber = $"MR{bill.MonthlyInvoiceId:D7}", BillNumber = bill.Number,
            StudentName = bill.StudentProfile.FullName,
            GuardianName = bill.StudentProfile.FullName + " (tuition account)",
            TuitionPeriod = bill.Month.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            CompletedAt = bill.PaidAtUtc.Value, PaymentProvider = bill.Total == 0 ? "Free" : "Stripe",
            Currency = "MYR", AmountPaid = bill.Total, TotalAmount = bill.Total, TuitionSubtotal = bill.Total,
            BackController = "MonthlyBilling", BackAction = "Details", BackId = bill.MonthlyInvoiceId,
            Items = bill.Lines.OrderBy(x => x.MonthlyInvoiceLineId).Select(x => new PaymentReceiptItemViewModel {
                Description = x.Description, Quantity = 1, UnitAmount = x.Amount, LineTotal = x.Amount }).ToList()
        });
    }

    [Authorize(Roles="Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(CancellationToken ct)
    {
        try { int count=await billing.GenerateCurrentAsync(ct); TempData["SuccessMessage"]=$"Created {count} current-month bill(s). Existing and prepaid months were not billed again."; }
        catch (Exception e) when (e is not OperationCanceledException) { logger.LogError(e,"Monthly bill generation failed"); TempData["ErrorMessage"]="Generation could not finish. Existing bills are preserved; review the log and retry."; }
        return RedirectToAction(nameof(Index));
    }
    [Authorize(Roles="Parent"), HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("PaymentPolicy")]
    public async Task<IActionResult> Pay(int id, CancellationToken ct)
    {
        if (!await billing.Accessible(User).AnyAsync(x => x.MonthlyInvoiceId == id, ct)) return NotFound();
        try { return Redirect(await payments.CheckoutAsync(id, ct)); }
        catch (Exception e) when (e is not OperationCanceledException)
        { logger.LogWarning(e,"Monthly checkout unavailable for bill {Id}",id); TempData["ErrorMessage"]="Checkout could not be opened. Check payment status before trying again; contact the centre if it remains unavailable."; return RedirectToAction(nameof(Details),new {id}); }
    }
    [Authorize(Roles="Admin,Parent"), HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("PaymentPolicy")]
    public async Task<IActionResult> CheckPayment(int id, CancellationToken ct)
    {
        if (!await billing.Accessible(User).AnyAsync(x => x.MonthlyInvoiceId == id, ct)) return NotFound();
        try { await payments.VerifyAsync(id,ct); TempData["SuccessMessage"]="Payment verified. This bill's overdue restriction is cleared."; }
        catch (Exception e) when (e is not OperationCanceledException)
        { logger.LogWarning(e,"Monthly payment not confirmed for bill {Id}",id); TempData["ErrorMessage"]="Payment has not been confirmed. Please wait for the payment provider or contact the centre. Do not pay again if money was deducted."; }
        return RedirectToAction(nameof(Details),new {id});
    }
}
