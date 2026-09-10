using Microsoft.AspNetCore.Mvc;

namespace SecureSmartTuitionSystem.Controllers
{
    public class StripeWebhookController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
