using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SecureSmartTuitionSystem.Services.Security;
namespace SecureSmartTuitionSystem.Filters;
public sealed class SiteErrorResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (SiteErrorResponses.WantsJson(context.HttpContext.Request)) return;
        int? status = context.Result switch {
            StatusCodeResult r => r.StatusCode,
            ObjectResult r when r.Value is string || r.Value == null => r.StatusCode,
            ContentResult r => r.StatusCode,
            _ => null
        };
        if (status is >= 400 and <= 599)
            context.Result = SiteErrorResponses.Result(context.HttpContext, status.Value);
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
