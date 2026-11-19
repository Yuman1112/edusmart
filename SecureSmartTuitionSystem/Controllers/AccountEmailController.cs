using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.Models.Security;
namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin,Tutor,Parent,Student")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("AccountEmail")]
[RequestSizeLimit(16384)]
public sealed class AccountEmailController(UserManager<ApplicationUser> users, AccountEmailService service, SecurityEventService events) : Controller
{
    public sealed class BeginInput
    {
        [Required, EmailAddress, StringLength(254)] public string NewEmail { get; set; } = "";
        [Required, StringLength(128), DataType(DataType.Password)] public string Password { get; set; } = "";
    }
    public sealed record PageModel(string UserName, string? CurrentEmail, string ProfileController, EmailChangeRequest? Pending, string? DraftEmail = null);
    public sealed record ConfirmationModel(string? Token, string? NewEmail, string Message, bool CanConfirm, bool Success, string? UserName = null);
    private string ProfileController => User.IsInRole("Admin") ? "Admin" : User.IsInRole("Tutor") ? "Tutor" : User.IsInRole("Student") ? "Student" : "Parent";
    private async Task<ApplicationUser?> OwnAsync()
    {
        var user = await users.GetUserAsync(User); return await service.UsableAsync(user) ? user : null;
    }
    private bool Allow(ApplicationUser user, string operation)
    {
        var wait = TwoFactorRequestLimits.TryAcquire("email:" + user.Id, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", operation);
        if (wait == 0) return true;
        Response.Headers["Retry-After"] = wait.ToString(System.Globalization.CultureInfo.InvariantCulture); return false;
    }
    private IActionResult Feedback(EmailOperation result)
    { TempData[result.Success ? "EmailSuccess" : "EmailError"] = result.Message; return RedirectToAction(nameof(Index)); }
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await OwnAsync(); if (user == null) return Forbid();
        ViewData["RequestCooldown"] = AccountRequestCooldown.Read("email:" + user.Id, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        return View(new PageModel(user.UserName ?? "", user.Email, ProfileController, await service.PendingAsync(user.Id), TempData["EmailDraft"] as string));
    }
    [HttpPost("Start"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(BeginInput input)
    {
        var user = await OwnAsync(); if (user == null) return Forbid();
        if (!Allow(user, "password")) return RedirectToAction(nameof(Index));
        if (!ModelState.IsValid) return Feedback(new(false, "Enter your current password and a valid new email address."));
        if (!await users.CheckPasswordAsync(user, input.Password)) return Feedback(new(false, "Incorrect current password."));
        if (!Allow(user, "send")) return RedirectToAction(nameof(Index));
        var result = await service.StartAsync(user, input.NewEmail);
        if (result.Success) await events.RecordAsync(HttpContext, "EmailChangeRequested", SecuritySeverity.Information, "An account email change was requested.");
        return Feedback(result);
    }
    [HttpPost("VerifyCurrent"), ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyCurrent(Guid requestId, string? code)
    {
        var user = await OwnAsync(); if (user == null) return Forbid();
        if (!Allow(user, "verify")) return RedirectToAction(nameof(Index));
        return Feedback(await service.VerifyCurrentAsync(user, requestId, code));
    }
    [HttpPost("Resend"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(Guid requestId)
    {
        var user = await OwnAsync(); if (user == null) return Forbid();
        if (!Allow(user, "send")) return RedirectToAction(nameof(Index));
        return Feedback(await service.ResendAsync(user, requestId));
    }
    [HttpPost("Cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid requestId, bool changeAddress = false)
    {
        var user = await OwnAsync(); if (user == null) return Forbid();
        var pending = await service.PendingAsync(user.Id);
        var result = await service.CancelAsync(user, requestId);
        var draft = changeAddress && pending?.Id == requestId ? pending.NewEmail : null;
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            if (!result.Success) return Conflict(new { message = result.Message });
            TempData["EmailSuccess"] = "Email change request cancelled. Your current email is unchanged.";
            ViewData["RequestCooldown"] = AccountRequestCooldown.Read("email:" + user.Id, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            return PartialView("_EmailFlow", new PageModel(user.UserName ?? "", user.Email, ProfileController, await service.PendingAsync(user.Id), draft));
        }
        if (result.Success && draft != null) TempData["EmailDraft"] = draft;
        return Feedback(result);
    }
    [AllowAnonymous, HttpGet("Confirm")]
    public async Task<IActionResult> Confirm(string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var row = await service.ConfirmationAsync(token);
        var owner = row == null ? null : await users.FindByIdAsync(row.UserId);
        return View(new ConfirmationModel(row == null ? null : token, row?.NewEmail,
            row == null ? "This link is invalid, expired or replaced. Request a new link from your profile." : "Confirm this address as your new EduSmart account email.", row != null, false, owner?.UserName));
    }
    [AllowAnonymous, HttpPost("Confirm"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var outcome = await service.ConfirmAsync(token);
        if (outcome.Result.Success)
        {
            await events.RecordAsync(HttpContext, "EmailChanged", SecuritySeverity.Information, "The verified account email was updated.", outcome.UserId);
            if (users.GetUserId(User) == outcome.UserId)
            {
                HttpContext.Session.Clear();
                await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
                await HttpContext.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);
                Response.Cookies.Delete(".EduSmart.Session");
            }
        }
        var owner = outcome.UserId == null ? null : await users.FindByIdAsync(outcome.UserId);
        return View("Confirm", new ConfirmationModel(null, owner?.Email, outcome.Result.Message, false, outcome.Result.Success, owner?.UserName));
    }
}
