using Microsoft.AspNetCore.Mvc;

namespace SecureSmartTuitionSystem.Controllers
{
    public class PaymentsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
