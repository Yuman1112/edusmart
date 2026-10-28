using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using SecureSmartTuitionSystem.Services.Billing;
namespace SecureSmartTuitionSystem.Filters;
public sealed class BillingViewOnlyFilter(MonthlyBillingService billing) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method=context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) ||
            context.ActionDescriptor is not ControllerActionDescriptor action || context.HttpContext.User.IsInRole("Admin") ||
            (!context.HttpContext.User.IsInRole("Parent") && !context.HttpContext.User.IsInRole("Student"))) { await next(); return; }
        // Reading, payment, account access, support, read markers and withdrawals
        // stay available. Do not lock a debtor out of the route needed to settle.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Payments", "MonthlyBilling", "Messages", "Faq", "NotificationCenter", "Announcements", "EnrollmentExits", "Home" };
        if (!allowed.Contains(action.ControllerName) && await billing.HasRestrictionAsync(context.HttpContext.User,context.HttpContext.RequestAborted))
        {
            if (context.Controller is Controller controller) controller.TempData["ErrorMessage"]="An overdue monthly tuition bill restricts new enrollment and academic changes. You can still view records, pay, withdraw and contact the centre.";
            context.Result=new RedirectToActionResult("Index","MonthlyBilling",null); return;
        }
        await next();
    }
}
