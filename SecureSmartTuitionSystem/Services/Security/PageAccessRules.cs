using System.Security.Claims;
namespace SecureSmartTuitionSystem.Services.Security;
public static class PageAccessRules
{
    public static readonly string[] ApplicationRoles = ["Admin", "Tutor", "Parent", "Student"];
    private static readonly HashSet<string> EnabledIdentityPages = new(StringComparer.OrdinalIgnoreCase) {
        "/Account/Login", "/Account/LoginWith2fa", "/Account/Logout", "/Account/AccessDenied",
        "/Account/ForgotPassword", "/Account/ResetPassword", "/Account/SetupPassword", "/Account/ChangePassword"
    };
    public static bool CanUsePublicSupport(ClaimsPrincipal user) => user.Identity?.IsAuthenticated != true ||
        (!user.IsInRole("Admin") && new[] { "Parent", "Student", "Tutor" }.Any(user.IsInRole));
    public static bool IsEnabledIdentityPage(string? page) => page != null && EnabledIdentityPages.Contains(page);
}
