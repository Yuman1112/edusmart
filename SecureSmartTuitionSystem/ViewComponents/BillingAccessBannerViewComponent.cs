using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Services.Billing;
namespace SecureSmartTuitionSystem.ViewComponents;
public sealed class BillingAccessBannerViewComponent(MonthlyBillingService billing) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true || !await billing.HasRestrictionAsync(HttpContext.User,HttpContext.RequestAborted)) return Content("");
        return View();
    }
}
