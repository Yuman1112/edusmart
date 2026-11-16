using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("PasswordResetPolicy")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ForgotPasswordModel(UserManager<ApplicationUser> users, AccountSecurityMail mail,
    IConfiguration configuration, IWebHostEnvironment environment, ILogger<ForgotPasswordModel> logger,
    IEmailOtpRecipientResolver recipients, AccountEmailLinks links) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [BindProperty, StringLength(256)]
    public string? UserId { get; set; }

    [TempData]
    public bool RequestReceived { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var normalized = users.NormalizeEmail(Email.Trim());
        ApplicationUser? user = null;
        EmailOtpRecipient? recipient = null;
        if (!string.IsNullOrWhiteSpace(UserId))
        {
            // A guardian address alone must never select one of their children.
            user = await users.FindByNameAsync(UserId.Trim());
            if (user != null) recipient = await recipients.ResolveAsync(user, true, HttpContext.RequestAborted);
            if (recipient == null || users.NormalizeEmail(recipient.Address) != normalized) user = null;
        }
        else
        {
            var matches = await users.Users.Where(u => u.NormalizedEmail == normalized && u.IsActive && !u.MustChangePassword)
                .Take(2).ToListAsync(HttpContext.RequestAborted);
            if (matches.Count == 1)
            {
                user = matches[0]; recipient = await recipients.ResolveAsync(user, true, HttpContext.RequestAborted);
            }
        }
        if (user != null && recipient != null && user.IsActive && !user.MustChangePassword && await users.HasPasswordAsync(user))
        {
            var origin = configuration["App:PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(origin) && environment.IsDevelopment())
                origin = "https://localhost:7086";

            if (Uri.TryCreate(origin, UriKind.Absolute, out var baseUri) && baseUri.Scheme == Uri.UriSchemeHttps
                && string.IsNullOrEmpty(baseUri.UserInfo) && baseUri.AbsolutePath == "/"
                && string.IsNullOrEmpty(baseUri.Query) && string.IsNullOrEmpty(baseUri.Fragment))
            {
                var identityCode = await users.GeneratePasswordResetTokenAsync(user);
                var code = links.ProtectRecovery(new(user.Id, identityCode, EmailChangeRules.RecipientHash(recipient)));
                var path = Url.Page("./ResetPassword", new { account = user.Id, code });
                if (path != null)
                {
                    try
                    {
                        await mail.SendAsync(
                            recipient.Address,
                            "EduSmart - Reset Your Password",
                            SecurityEmailTemplates.RecoveryEmail(
                                new Uri(baseUri, path).AbsoluteUri, user.UserName ?? "", recipient.GuardianLinkId.HasValue),
                            HttpContext.RequestAborted,
                            isHtml: true);
                    }
                    catch (Exception ex) when (ex is System.Net.Mail.SmtpException or InvalidOperationException
                        or FormatException or ArgumentException)
                    {
                        logger.LogWarning("Password reset email could not be sent ({ErrorType}).", ex.GetType().Name);
                    }
                }
            }
            else logger.LogWarning("Password reset requires App:PublicBaseUrl to be an HTTPS origin.");
        }

        RequestReceived = true;
        return RedirectToPage();
    }
}
