using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.Services.Communication;
using SecureSmartTuitionSystem.ViewModels.Communication;
namespace SecureSmartTuitionSystem.Controllers;
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GuestSupportController(ApplicationDbContext db, IMessageProtectionService protection) : Controller
{
    [HttpGet] public IActionResult Index() => View(new GuestSupportForm { SubmissionKey = Guid.NewGuid() });
    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("GuestEnquiryPolicy")]
    public async Task<IActionResult> Index(GuestSupportForm model, CancellationToken ct)
    {
        model.Name = (model.Name ?? "").Trim();
        model.Email = (model.Email ?? "").Trim();
        model.Message = (model.Message ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(model.Website)) return BadRequest();
        if (model.SubmissionKey == Guid.Empty) ModelState.AddModelError("", "Please reopen the contact form and try again.");
        void FieldError(string key, string message)
        {
            if (!ModelState.TryGetValue(key, out var state) || state.Errors.Count == 0)
                ModelState.AddModelError(key, message);
        }
        if (model.Name.Length == 0) FieldError(nameof(model.Name), "Name field is required.");
        if (model.Email.Length == 0) FieldError(nameof(model.Email), "Email field is required.");
        else if (model.Email.Any(char.IsControl)) FieldError(nameof(model.Email), "Please enter a valid email address.");
        if (model.Message.Length == 0) FieldError(nameof(model.Message), "Message field is required.");
        else if (model.Message.Length < 5) FieldError(nameof(model.Message), "Message must contain at least 5 characters.");
        if (!model.Consent) FieldError(nameof(model.Consent), "This field is required.");
        if (!ModelState.IsValid) return View(model);
        try
        {
            if (!await db.GuestEnquiries.AnyAsync(x => x.SubmissionKey == model.SubmissionKey, ct))
            {
                db.GuestEnquiries.Add(new GuestEnquiry { SubmissionKey = model.SubmissionKey,
                    ProtectedContact = protection.ProtectText(JsonSerializer.Serialize(new GuestContact(model.Name, model.Email, model.Message))) });
                await db.SaveChangesAsync(ct);
            }
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627)
        {
            // Confirm the enquiry exists before reporting a simultaneous retry as successful.
            if (!await db.GuestEnquiries.AsNoTracking().AnyAsync(x => x.SubmissionKey == model.SubmissionKey, ct))
            {
                ModelState.AddModelError("", "Your enquiry could not be saved. Please try again later.");
                return View(model);
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.SqlClient.SqlException)
        {
            ModelState.AddModelError("", "Your enquiry could not be saved. Please try again later.");
            return View(model);
        }
        TempData["GuestSupportSaved"] = "Your enquiry has been saved for staff. Staff will reply directly to your email within 1–3 days.";
        return RedirectToAction(nameof(Index));
    }
}
