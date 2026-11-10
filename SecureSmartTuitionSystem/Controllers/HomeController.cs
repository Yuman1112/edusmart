using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SecureSmartTuitionSystem.Models;

namespace SecureSmartTuitionSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction(
                        "Index",
                        "Admin");
                }

                if (User.IsInRole("Parent"))
                {
                    return RedirectToAction("Index", "Parent");
                }

                if (User.IsInRole("Student"))
                {
                    return RedirectToAction("Index", "Student");
                }

                if (User.IsInRole("Tutor"))
                {
                    return RedirectToAction(
                        "Index",
                        "Tutor");
                }
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(
                new ErrorViewModel
                {
                    RequestId =
                        Activity.Current?.Id
                        ?? HttpContext.TraceIdentifier
                });
        }
    }
}