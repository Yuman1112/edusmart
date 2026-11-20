using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureSmartTuitionSystem.Services.Security;
namespace SecureSmartTuitionSystem.Middleware;
public sealed class PageAudienceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (!context.Items.ContainsKey(SiteErrorResponses.OriginalEndpoint))
            context.Items[SiteErrorResponses.OriginalEndpoint] = endpoint;
        if (endpoint == null) { context.Response.StatusCode = 404; return; }
        var action = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action?.ControllerName is "Faq" or "GuestSupport" && !PageAccessRules.CanUsePublicSupport(context.User))
        { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
        var page = endpoint?.Metadata.GetMetadata<PageActionDescriptor>();
        // Default Identity UI must not bypass the application's controlled account provisioning.
        if (page?.AreaName == "Identity" && !PageAccessRules.IsEnabledIdentityPage(page.ViewEnginePath))
        { context.Items["Security.DisabledIdentityPage"] = true; context.Response.StatusCode = 404; return; }
        await next(context);
    }
}
