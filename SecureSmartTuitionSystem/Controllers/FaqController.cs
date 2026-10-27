using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecureSmartTuitionSystem.Services.Communication;
using SecureSmartTuitionSystem.ViewModels.Communication;

namespace SecureSmartTuitionSystem.Controllers;

[Authorize(Roles = "Admin,Parent,Student,Tutor")]
[EnableRateLimiting("PublicFormPolicy")]
public sealed class FaqController : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new FaqViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Index(FaqViewModel model)
    {
        model.Searched = true;
        if (!ModelState.IsValid) return View(model);
        if (!string.IsNullOrWhiteSpace(model.Topic))
            model.Answer = FaqService.FindById(model.Topic);
        else if (!string.IsNullOrWhiteSpace(model.Question))
            model.Answer = FaqService.Match(model.Question);
        else
            ModelState.AddModelError(nameof(model.Question), "Choose a question or type a short question.");
        return View(model);
    }
}
