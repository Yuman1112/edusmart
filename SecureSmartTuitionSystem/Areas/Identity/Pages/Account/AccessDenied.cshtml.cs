using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SecureSmartTuitionSystem.Areas.Identity.Pages.Account
{
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class AccessDeniedModel : PageModel
    {
        public Microsoft.AspNetCore.Mvc.IActionResult OnGet() =>
            SecureSmartTuitionSystem.Services.Security.SiteErrorResponses.Result(HttpContext, 403);
    }
}
