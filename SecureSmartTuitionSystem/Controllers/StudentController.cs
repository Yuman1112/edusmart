using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.ViewModels.Identity;
using SecureSmartTuitionSystem.ViewModels.Student;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser>
            _userManager;

        public StudentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public Task<IActionResult> MyProfile([FromServices] AccountProfileService profiles) =>
            ShowProfileAsync(profiles, editing: false);

        [HttpGet]
        public Task<IActionResult> EditProfile([FromServices] AccountProfileService profiles) =>
            ShowProfileAsync(profiles, editing: true);

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(
            [FromServices] AccountProfileService profiles,
            [Bind("FullName,School")] AccountProfileModel input)
        {
            var account = await _userManager.GetUserAsync(User);
            if (account == null || await profiles.LoadAsync(account, "Student") is not { } model)
                return NotFound();

            if (string.IsNullOrWhiteSpace(input.School))
                ModelState.AddModelError(nameof(input.School), "Please enter your school.");

            if (ModelState.IsValid)
            {
                if (await profiles.SaveAsync(account, model, input))
                {
                    TempData["ProfileSaved"] = "Your profile was updated successfully.";
                    return RedirectToAction(nameof(MyProfile));
                }

                ModelState.AddModelError("", "Your account changed while you were editing. Reload and try again.");
            }

            ViewData["Editing"] = true;
            return View("AccountProfile", model);
        }

        private async Task<IActionResult> ShowProfileAsync(AccountProfileService profiles, bool editing)
        {
            var account = await _userManager.GetUserAsync(User);
            if (account == null || await profiles.LoadAsync(account, "Student") is not { } model)
                return NotFound();

            ViewData["Editing"] = editing;
            return View("AccountProfile", model);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index()
        {
            if (string.IsNullOrWhiteSpace(_userManager.GetUserId(User))) return Challenge();
            var model = await new SecureSmartTuitionSystem.Services.Student.StudentDashboardService(_context).BuildAsync(User);
            return model == null ? NotFound() : View(model);
        }
    }
}
