using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Tutor;

namespace SecureSmartTuitionSystem.Controllers
{
	[Authorize(Roles = "Tutor")]
	public class TutorController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly UserManager<ApplicationUser> _userManager;

		public TutorController(
			ApplicationDbContext context,
			UserManager<ApplicationUser> userManager)
		{
			_context = context;
			_userManager = userManager;
		}

		public async Task<IActionResult> Index()
		{
			var user = await _userManager.GetUserAsync(User);

			if (user == null)
			{
				return Challenge();
			}

			var assignedClasses =
				_context.ClassSessions
					.AsNoTracking()
					.Where(session =>
						session.TutorUserId == user.Id &&
						session.Status ==
							ClassSessionStatus.Active);

			var classIds =
				await assignedClasses
					.Select(session =>
						session.ClassSessionId)
					.ToListAsync();

			var model = new TutorDashboardViewModel
			{
				TutorName =
					user.FullName ??
					user.Email ??
					"Tutor",

				AssignedClassCount =
					classIds.Count,

				TodayClassCount =
					await assignedClasses.CountAsync(
						session =>
							session.DayOfWeek ==
								DateTime.Today.DayOfWeek),

				ActiveStudentCount =
					await _context.StudentEnrollments
						.Where(enrollment =>
							classIds.Contains(
								enrollment.ClassSessionId) &&
							enrollment.Status ==
								StudentEnrollmentStatus.Active)
						.Select(enrollment =>
							enrollment.StudentProfileId)
						.Distinct()
						.CountAsync(),

				Classes =
					await assignedClasses
						.Include(session => session.Subject)
						.OrderBy(session => session.DayOfWeek)
						.ThenBy(session => session.StartTime)
						.Select(session =>
							new TutorClassItemViewModel
							{
								ClassSessionId =
									session.ClassSessionId,

								ClassCode =
									session.ClassCode,

								SubjectName =
									session.Subject.SubjectName,

								AcademicLevel =
									session.Subject.AcademicLevel,

								Day =
									session.DayOfWeek.ToString(),

								Time =
									session.StartTime
										.ToString("hh:mm tt") +
									" - " +
									session.EndTime
										.ToString("hh:mm tt"),

								Venue =
									session.Venue,

								StudentCount =
									_context.StudentEnrollments
										.Count(enrollment =>
											enrollment.ClassSessionId ==
												session.ClassSessionId &&
											enrollment.Status ==
												StudentEnrollmentStatus
													.Active)
							})
						.ToListAsync()
			};

			return View(model);
		}
	}
}