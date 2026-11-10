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
    IConfiguration configuration, IWebHostEnvironment environment, ILogger<ForgotPasswordModel> logger) : PageModel
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
        var accounts = users.Users.Where(u => u.NormalizedEmail == normalized && u.IsActive && !u.MustChangePassword);
        if (!string.IsNullOrWhiteSpace(UserId))
        {
            var name = users.NormalizeName(UserId.Trim());
            accounts = accounts.Where(u => u.NormalizedUserName == name);
        }

        var matches = await accounts.Take(2).ToListAsync(HttpContext.RequestAborted);
        if (matches.Count == 1 && await users.HasPasswordAsync(matches[0]))
        {
            var user = matches[0];
            var origin = configuration["App:PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(origin) && environment.IsDevelopment())
                origin = "https://localhost:7086";

            if (Uri.TryCreate(origin, UriKind.Absolute, out var baseUri) && baseUri.Scheme == Uri.UriSchemeHttps
                && string.IsNullOrEmpty(baseUri.UserInfo) && baseUri.AbsolutePath == "/"
                && string.IsNullOrEmpty(baseUri.Query) && string.IsNullOrEmpty(baseUri.Fragment))
            {
                var code = await users.GeneratePasswordResetTokenAsync(user);
                var path = Url.Page("./ResetPassword", new { account = user.Id, code });
                if (path != null)
                {
                    try
                    {
                        await mail.SendAsync(
                            user.Email!,
                            "EduSmart - Reset Your Password",
                            SecurityEmailTemplates.ForgotPassword(
                                new Uri(baseUri, path).AbsoluteUri),
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
