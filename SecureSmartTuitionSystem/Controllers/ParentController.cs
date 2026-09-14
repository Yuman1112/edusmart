using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
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