using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Services.Security;
namespace SecureSmartTuitionSystem.Controllers;
[AllowAnonymous, IgnoreAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SiteErrorsController : Controller
{
    // Read-only and accepts the original verb during status/exception re-execution.
    [Route("Errors/{statusCode:int}")]
    public IActionResult Status(int statusCode) => SiteErrorResponses.Result(HttpContext,
        statusCode is >= 400 and <= 599 ? statusCode : 500);
}
