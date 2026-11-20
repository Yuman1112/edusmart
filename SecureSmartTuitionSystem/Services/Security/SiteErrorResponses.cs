using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using SecureSmartTuitionSystem.Models;
namespace SecureSmartTuitionSystem.Services.Security;

public static class SiteErrorResponses
{
    public const string OriginalEndpoint = "EduSmart.Error.OriginalEndpoint";
    public static bool WantsJson(HttpRequest request) =>
        request.Path.StartsWithSegments("/api") || request.Path.StartsWithSegments("/Session") ||
        request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
        (request.Headers.Accept.ToString().Contains("json", StringComparison.OrdinalIgnoreCase) &&
         !request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase));

    public static ErrorViewModel Create(HttpContext context, int status)
    {
        var endpoint = context.Items[OriginalEndpoint] as Endpoint ?? context.GetEndpoint();
        var action = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        bool paymentLink = action?.ControllerName == "Payments" &&
            action.ActionName is "Checkout" or "CreateCheckoutSession" or "Success" or "ConfirmPayment" or "ReceiptBySession" &&
            status is 400 or 404 or 410;
        var (title, message) = status switch {
            400 => ("Request could not be accepted", "Please reopen the form and try again. Its security token or submitted information may no longer be valid."),
            401 => ("Sign in required", "Your sign-in session is missing or has expired. Sign in again to continue."),
            403 => ("Access denied", "Your account does not have permission to access this page or perform this action."),
            404 => ("Page or record unavailable", "This page or record does not exist, is no longer available, or is outside your permitted access."),
            405 => ("Action not available", "This address does not support that action. Please use the buttons on the website."),
            410 => ("Link no longer available", "This link can no longer be used. Please request a new link from the centre."),
            413 => ("Upload too large", "The uploaded file exceeds the permitted size. Choose a smaller supported file."),
            429 => ("Too many requests", "Please wait a moment before trying again."),
            503 => ("Service temporarily unavailable", "The service is temporarily unavailable. Please try again later."),
            _ => ("Something went wrong", "We could not complete your request. Please try again later or contact the centre with the reference below.")
        };
        return new ErrorViewModel { StatusCode = status, ErrorCode = paymentLink ? "PAYMENT_LINK_UNAVAILABLE" : $"HTTP_{status}",
            Title = paymentLink ? "Payment link unavailable" : title,
            Message = paymentLink ? "This payment link cannot be verified. It may be invalid, expired or no longer available. If you have already paid, do not pay again or request another payment link: sign in to check your bill and receipt. If payment is still due, contact Admin to confirm the next step. This page does not determine whether a payment was received." : message,
            RequestId = context.TraceIdentifier };
    }
    public static IActionResult Result(HttpContext context, int status)
    {
        var model = Create(context, status);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (WantsJson(context.Request)) return new ObjectResult(new ProblemDetails {
            Status = status, Title = model.Title, Detail = model.Message,
            Extensions = { ["code"] = model.ErrorCode, ["traceId"] = model.RequestId }
        }) { StatusCode = status, ContentTypes = { "application/problem+json" } };
        return new ViewResult { ViewName = "~/Views/Shared/Error.cshtml", StatusCode = status,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<ErrorViewModel>(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary()) { Model = model } };
    }
}
