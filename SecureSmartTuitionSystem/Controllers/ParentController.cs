using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.ViewModels.Identity;
using SecureSmartTuitionSystem.ViewModels.Parent;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Parent")]
    public class ParentController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser>
            _userManager;

        public ParentController(
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
            [Bind("FullName,PhoneNumber,Address")] AccountProfileModel input)
        {
            var account = await _userManager.GetUserAsync(User);
            if (account == null || await profiles.LoadAsync(account, "Parent") is not { } model)
                return NotFound();

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
            if (account == null || await profiles.LoadAsync(account, "Parent") is not { } model)
                return NotFound();

            ViewData["Editing"] = editing;
            return View("AccountProfile", model);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string? currentUserId =
                _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(
                    currentUserId))
            {
                return Challenge();
            }

            var parentProfile =
                await _context.ParentProfiles
                    .AsNoTracking()
                    .Include(parent =>
                        parent.ApplicationUser)
                    .Include(parent =>
                        parent.ParentStudents)
                        .ThenInclude(link =>
                            link.StudentProfile)
                            .ThenInclude(student =>
                                student.ApplicationUser)
                    .FirstOrDefaultAsync(parent =>
                        parent.ApplicationUserId ==
                            currentUserId);

            if (parentProfile == null)
            {
                return NotFound();
            }

            var model =
                new ParentDashboardViewModel
                {
                    ParentName =
                        parentProfile.FullName,

                    ParentUserName =
                        parentProfile.ApplicationUser
                            .UserName
                        ?? string.Empty,

                    Students =
                        parentProfile.ParentStudents
                            .Where(link =>
                                link.IsActive)
                            .Select(link =>
                                new ParentDashboardStudentViewModel
                                {
                                    StudentProfileId =
                                        link.StudentProfileId,

                                    StudentUserName =
                                        link.StudentProfile
                                            .ApplicationUser?
                                            .UserName
                                        ?? link.StudentProfile
                                            .StudentNumber,

                                    StudentName =
                                        link.StudentProfile
                                            .FullName,

                                    School =
                                        link.StudentProfile
                                            .School,

                                    AcademicLevel =
                                        link.StudentProfile
                                            .AcademicLevel,

                                    Relationship =
                                        link.Relationship
                                        ?? "Child"
                                })
                            .OrderBy(student =>
                                student.StudentName)
                            .ToList()
                };

            return View(model);
        }
    }
}