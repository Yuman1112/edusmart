using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SecureSmartTuitionSystem.Filters;

/// <summary>
/// Small helper so Controllers read the authenticated user's id/role from
/// claims consistently. The claims themselves are issued by the User
/// Authentication &amp; RBAC Module (teammate's) at login; this project only
/// consumes them.
/// </summary>
public static class CurrentUserExtensions
{
    public static string? CurrentUserId(this Controller controller) =>
        controller.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string? CurrentUserRole(this Controller controller) =>
        controller.User.FindFirstValue(ClaimTypes.Role);
}
