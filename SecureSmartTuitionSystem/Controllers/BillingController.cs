using Microsoft.AspNetCore.Mvc;

namespace SecureSmartTuitionSystem.Controllers
{
    public class BillingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
