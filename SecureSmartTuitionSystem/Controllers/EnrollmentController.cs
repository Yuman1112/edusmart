using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Enrollment;
using SecureSmartTuitionSystem.Services.Enrollment;

namespace SecureSmartTuitionSystem.Controllers
{
    [EnableRateLimiting("PublicFormPolicy")]
    public class EnrollmentController : Controller
    {
        private const string StudentDetailsSessionKey =
            "Enrollment.StudentDetails";

        private const string GuardianDetailsSessionKey =
            "Enrollment.GuardianDetails";

        private const string ExistingParentUserIdSessionKey =
            "Enrollment.ExistingParentUserId";

        private const string LastSubmittedApplicationIdSessionKey =
            "Enrollment.LastSubmittedApplicationId";

        private GuardianEnrollmentVerification GuardianVerification => HttpContext.RequestServices.GetRequiredService<GuardianEnrollmentVerification>();

        private readonly UserManager<ApplicationUser>
            _userManager;

        private readonly ApplicationDbContext
            _context;

        public EnrollmentController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        /*
         * =====================================================
         * START NEW ENROLLMENT
         * =====================================================
         */

        [Authorize(Roles = "Admin,Parent,Student"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubjects(int studentId)
        {
            if (User.Identity?.IsAuthenticated != true) return Challenge();
            if (!User.IsInRole("Admin") && !User.IsInRole("Parent") && !User.IsInRole("Student")) return Forbid();
            string? userId = _userManager.GetUserId(User);
            var student = await _context.StudentProfiles.Include(x => x.ApplicationUser).Include(x => x.ParentStudents).ThenInclude(x => x.ParentProfile).ThenInclude(x => x.ApplicationUser)
                .FirstOrDefaultAsync(x => x.StudentProfileId == studentId && x.IsActive &&
                    (User.IsInRole("Admin") || (User.IsInRole("Student") && x.ApplicationUserId == userId) || (User.IsInRole("Parent") && x.ParentStudents.Any(link => link.IsActive && link.ParentProfile.ApplicationUserId == userId))));
            if (student == null) return NotFound();
            var parent = student.ParentStudents.Where(x => x.IsActive && x.ParentProfile.ApplicationUser.IsActive && (User.IsInRole("Admin") || User.IsInRole("Student") || x.ParentProfile.ApplicationUserId == userId)).OrderByDescending(x => x.IsPrimaryGuardian).ThenBy(x => x.ParentProfileId).Select(x => x.ParentProfile).FirstOrDefault();
            if (parent == null) { TempData["ErrorMessage"] = "An active guardian link is required before adding subjects."; return RedirectToAction("Index", "LearningClasses"); }
            var details = new StudentDetailsViewModel { StudentName = student.FullName, StudentIdentificationNumber = student.IdentificationNumber, StudentDateOfBirth = student.DateOfBirth,
                StudentGender = student.Gender, StudentSchool = student.School, StudentEmail = student.ApplicationUser?.Email, StudentHasNoEmail = string.IsNullOrWhiteSpace(student.ApplicationUser?.Email) };
            var guardian = new GuardianDetailsViewModel { UseExistingParentAccount = true, ExistingParentVerified = true, ExistingParentEmail = parent.ApplicationUser.Email,
                VerifiedParentName = parent.FullName, VerifiedParentEmail = parent.ApplicationUser.Email };
            HttpContext.Session.SetString(StudentDetailsSessionKey, JsonSerializer.Serialize(details)); HttpContext.Session.SetString(GuardianDetailsSessionKey, JsonSerializer.Serialize(guardian));
            HttpContext.Session.SetString(ExistingParentUserIdSessionKey, parent.ApplicationUserId); HttpContext.Session.SetInt32("Enrollment.AddSubjectStudentId", student.StudentProfileId);
            HttpContext.Session.Remove("Enrollment.SelectedClassIds"); HttpContext.Session.Remove("Enrollment.RequestedStartDate"); HttpContext.Session.Remove(LastSubmittedApplicationIdSessionKey);
            return RedirectToAction("SelectClasses", "EnrollmentSubmission");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Start()
        {
            HttpContext.Session.Remove("Enrollment.GuardianOtp.Pending");
            HttpContext.Session.Remove("Enrollment.GuardianOtp.Proof");
            /*
             * Clear any previous incomplete enrollment.
             */
            HttpContext.Session.Remove("Enrollment.AddSubjectStudentId");
            HttpContext.Session.Remove("Enrollment.RequestedStartDate");
            HttpContext.Session.Remove(
                StudentDetailsSessionKey);

            HttpContext.Session.Remove(
                GuardianDetailsSessionKey);

            HttpContext.Session.Remove(
                ExistingParentUserIdSessionKey);

            HttpContext.Session.Remove(
                LastSubmittedApplicationIdSessionKey);

            return RedirectToAction(
                nameof(StudentDetails));
        }

        /*
         * =====================================================
         * STEP 1 - STUDENT DETAILS GET
         * =====================================================
         */

        [AllowAnonymous]
        [HttpGet]
        public IActionResult StudentDetails()
        {
            if (HttpContext.Session.GetInt32("Enrollment.AddSubjectStudentId").HasValue)
                return RedirectToAction("SelectClasses", "EnrollmentSubmission");
            string? json =
                HttpContext.Session.GetString(
                    StudentDetailsSessionKey);

            var model =
                string.IsNullOrWhiteSpace(json)
                    ? new StudentDetailsViewModel()
                    : JsonSerializer.Deserialize<
                        StudentDetailsViewModel>(json)
                      ?? new StudentDetailsViewModel();

            return View(model);
        }

        /*
         * =====================================================
         * CHECK EXISTING STUDENT IC
         * =====================================================
         */
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> CheckStudentIc(
            string icNumber)
        {
            if (string.IsNullOrWhiteSpace(icNumber) ||
                !System.Text.RegularExpressions.Regex.IsMatch(
                    icNumber,
                    @"^\d{6}-\d{2}-\d{4}$"))
            {
                return Json(new
                {
                    exists = false
                });
            }

            string normalizedIc =
                icNumber.Trim();

            bool exists =
                await _context.StudentProfiles
                    .AsNoTracking()
                    .AnyAsync(student =>
                        student.IdentificationNumber ==
                            normalizedIc);

            return Json(new
            {
                exists
            });
        }

        /*
         * =====================================================
         * STEP 1 - STUDENT DETAILS POST
         * =====================================================
         */

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudentDetails(
            StudentDetailsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Normalize identification number
            var identificationNumber =
                model.StudentIdentificationNumber.Trim();

            // Check whether the same identification already exists
            var existingStudent =
                await _context.StudentProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        student =>
                            student.IdentificationNumber ==
                                identificationNumber);

            if (existingStudent != null)
            {
                ModelState.AddModelError(
                    nameof(model.StudentIdentificationNumber),
                    "A student with this IC number already exists. " +
                    "Please sign in to EduSmart to enroll in additional subjects.");

                return View(model);
            }

            // Save normalized value back into the model
            model.StudentIdentificationNumber =
                identificationNumber;

            model.StudentName =
                model.StudentName.Trim();

            model.StudentGender =
                model.StudentGender.Trim();

            model.StudentSchool =
                model.StudentSchool.Trim();

            if (model.StudentHasNoEmail)
            {
                model.StudentEmail = null;
            }
            else
            {
                model.StudentEmail =
                    model.StudentEmail?.Trim();
            }

            HttpContext.Session.Remove(
                ExistingParentUserIdSessionKey);

            HttpContext.Session.Remove(
                GuardianDetailsSessionKey);

            HttpContext.Session.SetString(
                StudentDetailsSessionKey,
                JsonSerializer.Serialize(model));

            return RedirectToAction(
                nameof(GuardianDetails));
        }

        /*
         * =====================================================
         * STEP 2 - GUARDIAN DETAILS GET
         * =====================================================
         */

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GuardianDetails()
        {
            if (HttpContext.Session.GetInt32("Enrollment.AddSubjectStudentId").HasValue)
                return RedirectToAction("SelectClasses", "EnrollmentSubmission");
            if (!HasStudentDetails())
            {
                return ReturnToStudentDetails();
            }

            string? json =
                HttpContext.Session.GetString(
                    GuardianDetailsSessionKey);

            var model =
                string.IsNullOrWhiteSpace(json)
                    ? new GuardianDetailsViewModel()
                    : JsonSerializer.Deserialize<
                        GuardianDetailsViewModel>(json)
                      ?? new GuardianDetailsViewModel();

            if (User.IsInRole("Parent") || model.UseExistingParentAccount)
            {
                await LoadSignedInParent(model);
            }
            ViewData["GuardianOtpPending"] = GuardianVerification.HasPending(HttpContext);
            ViewData["GuardianResendAt"] = GuardianVerification.GetResendAtUnixSeconds(HttpContext);

            /*
             * This GET action must display the Guardian page.
             * Do not redirect to SelectClasses here.
             */
            return View(model);
        }

        /*
         * =====================================================
         * VERIFY EXISTING PARENT ACCOUNT
         * =====================================================
         */

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            VerifyExistingParent(
                GuardianDetailsViewModel model)
        {
            if (!HasStudentDetails())
            {
                return ReturnToStudentDetails();
            }

            ModelState.Clear();
            model.UseExistingParentAccount = true;
            model.ExistingParentVerified = false;
            model.VerifiedParentName = null;
            model.VerifiedParentEmail = null;
            string? error = await GuardianVerification.SendAsync(HttpContext,
                model.ExistingParentEmail, model.ExistingParentIdentificationLast4);
            model.EnrollmentOtp = null;
            HttpContext.Session.SetString(GuardianDetailsSessionKey, JsonSerializer.Serialize(model));
            if (error != null) ModelState.AddModelError(string.Empty, error);
            ViewData["GuardianOtpPending"] = GuardianVerification.HasPending(HttpContext);
            ViewData["GuardianResendAt"] = GuardianVerification.GetResendAtUnixSeconds(HttpContext);
            ViewData["GuardianOtpMessage"] = error == null ? "A verification code has been sent to your registered email. It expires in 5 minutes." : null;
            return View("GuardianDetails", model);
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmGuardianOtp(GuardianDetailsViewModel model)
        {
            if (!HasStudentDetails()) return ReturnToStudentDetails();
            ModelState.Clear();
            if (await GuardianVerification.VerifyAsync(HttpContext, model.EnrollmentOtp))
                return RedirectToAction(nameof(GuardianDetails));
            var saved = HttpContext.Session.GetString(GuardianDetailsSessionKey);
            model = saved == null ? new GuardianDetailsViewModel() : JsonSerializer.Deserialize<GuardianDetailsViewModel>(saved)!;
            model.UseExistingParentAccount = true;
            model.ExistingParentVerified = false;
            ModelState.AddModelError(string.Empty, "The verification code is invalid or expired. Please try again or request a new code.");
            ViewData["GuardianOtpPending"] = GuardianVerification.HasPending(HttpContext);
            ViewData["GuardianResendAt"] = GuardianVerification.GetResendAtUnixSeconds(HttpContext);
            return View("GuardianDetails", model);
        }

        // Resolve account ownership from the authenticated identity, never posted account details.
        private async Task<bool> LoadSignedInParent(GuardianDetailsViewModel model)
        {
            model.UseExistingParentAccount = true;
            model.ExistingParentVerified = false;
            model.VerifiedParentName = null;
            model.VerifiedParentEmail = null;
            HttpContext.Session.Remove(ExistingParentUserIdSessionKey);
            var parent = await GuardianVerification.GetVerifiedAsync(HttpContext);
            if (parent == null && User.Identity?.IsAuthenticated == true && User.IsInRole("Parent"))
            {
                string? userId = _userManager.GetUserId(User);
                parent = await _context.ParentProfiles.Include(x => x.ApplicationUser)
                    .SingleOrDefaultAsync(x => x.ApplicationUserId == userId);
            }
            if (parent?.ApplicationUser == null || !parent.ApplicationUser.IsActive
                || parent.ApplicationUser.MustChangePassword) return false;
            model.ExistingParentVerified = true;
            model.ExistingParentEmail = parent.ApplicationUser.Email;
            model.ExistingParentIdentificationLast4 = parent.IdentificationLast4;
            model.VerifiedParentName = parent.FullName;
            model.VerifiedParentEmail = parent.ApplicationUser.Email;
            model.GuardianName = null;
            model.GuardianEmail = null;
            model.GuardianPhoneNumber = null;
            model.GuardianIdentificationNumber = null;
            HttpContext.Session.SetString(ExistingParentUserIdSessionKey, parent.ApplicationUserId);
            HttpContext.Session.SetString(GuardianDetailsSessionKey, JsonSerializer.Serialize(model));
            return true;
        }

        /*
         * =====================================================
         * STEP 2 - BACK TO STUDENT DETAILS
         * =====================================================
         */

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BackToStudentDetails(
            GuardianDetailsViewModel model)
        {
            if (!HasStudentDetails())
            {
                return ReturnToStudentDetails();
            }

            if (model.UseExistingParentAccount)
            {
                string? existingParentUserId =
                    HttpContext.Session.GetString(
                        ExistingParentUserIdSessionKey);

                string? savedJson =
                    HttpContext.Session.GetString(
                        GuardianDetailsSessionKey);

                GuardianDetailsViewModel?
                    savedModel = null;

                if (!string.IsNullOrWhiteSpace(
                    savedJson))
                {
                    savedModel =
                        JsonSerializer.Deserialize<
                            GuardianDetailsViewModel>(
                                savedJson);
                }

                /*
                 * Preserve verified status only when the
                 * server-side verification Session exists.
                 */
                if (!string.IsNullOrWhiteSpace(
                        existingParentUserId) &&
                    savedModel != null &&
                    savedModel.ExistingParentVerified)
                {
                    model.ExistingParentVerified =
                        true;

                    model.VerifiedParentName =
                        savedModel.VerifiedParentName;

                    model.VerifiedParentEmail =
                        savedModel.VerifiedParentEmail;

                    model.ExistingParentEmail =
                        savedModel.ExistingParentEmail;

                    model
                        .ExistingParentIdentificationLast4 =
                        savedModel
                            .ExistingParentIdentificationLast4;
                }
                else
                {
                    model.ExistingParentVerified =
                        false;

                    model.VerifiedParentName =
                        null;

                    model.VerifiedParentEmail =
                        null;
                }

                /*
                 * Existing Parent flow does not use the
                 * New Parent fields.
                 */
                model.GuardianName =
                    null;

                model.GuardianEmail =
                    null;

                model.GuardianPhoneNumber =
                    null;

                model.GuardianIdentificationNumber =
                    null;
            }
            else
            {
                /*
                 * New Parent flow does not use the existing
                 * account verification.
                 */
                HttpContext.Session.Remove(
                    ExistingParentUserIdSessionKey);

                model.ExistingParentVerified =
                    false;

                model.ExistingParentEmail =
                    null;

                model
                    .ExistingParentIdentificationLast4 =
                    null;

                model.VerifiedParentName =
                    null;

                model.VerifiedParentEmail =
                    null;

                model.GuardianName =
                    model.GuardianName?.Trim();

                model.GuardianEmail =
                    model.GuardianEmail?.Trim();

                model.GuardianPhoneNumber =
                    model.GuardianPhoneNumber?.Trim();

                model.GuardianIdentificationNumber =
                    model
                        .GuardianIdentificationNumber?
                        .Trim();
            }

            /*
             * Save the Guardian draft before returning.
             */
            HttpContext.Session.SetString(
                GuardianDetailsSessionKey,
                JsonSerializer.Serialize(model));

            return RedirectToAction(
                nameof(StudentDetails));
        }

        /*
         * =====================================================
         * STEP 2 - GUARDIAN DETAILS POST
         * =====================================================
         */

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardianDetails(
            GuardianDetailsViewModel model)
        {
            if (!HasStudentDetails())
            {
                return ReturnToStudentDetails();
            }

            if (User.IsInRole("Parent") || model.UseExistingParentAccount)
            {
                ModelState.Clear();
                if (!await LoadSignedInParent(model))
                {
                    ModelState.AddModelError(string.Empty, "Please verify your parent / guardian account before continuing.");
                    ViewData["GuardianOtpPending"] = GuardianVerification.HasPending(HttpContext);
            ViewData["GuardianResendAt"] = GuardianVerification.GetResendAtUnixSeconds(HttpContext);
                    return View(model);
                }
            }

            /*
             * =================================================
             * EXISTING PARENT / GUARDIAN
             * =================================================
             */

            if (model.UseExistingParentAccount)
            {
                string? existingParentUserId =
                    HttpContext.Session.GetString(
                        ExistingParentUserIdSessionKey);

                /*
                 * The account must be verified before the
                 * user can continue.
                 */
                if (string.IsNullOrWhiteSpace(existingParentUserId))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Please verify your existing parent " +
                        "or guardian account before " +
                        "continuing.");

                    model.ExistingParentVerified =
                        false;

                    return View(model);
                }

                /*
                 * Restore verified information from Session.
                 * Do not trust hidden browser values.
                 */
                string? savedJson =
                    HttpContext.Session.GetString(
                        GuardianDetailsSessionKey);

                if (!string.IsNullOrWhiteSpace(
                    savedJson))
                {
                    var savedModel =
                        JsonSerializer.Deserialize<
                            GuardianDetailsViewModel>(
                                savedJson);

                    if (savedModel != null &&
                        savedModel
                            .ExistingParentVerified)
                    {
                        model.ExistingParentEmail =
                            savedModel
                                .ExistingParentEmail;

                        model
                            .ExistingParentIdentificationLast4 =
                            savedModel
                                .ExistingParentIdentificationLast4;

                        model.ExistingParentVerified =
                            true;

                        model.VerifiedParentName =
                            savedModel.VerifiedParentName;

                        model.VerifiedParentEmail =
                            savedModel.VerifiedParentEmail;
                    }
                }

                if (!model.ExistingParentVerified)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "The parent or guardian account " +
                        "verification has expired. Please " +
                        "verify the account again.");

                    return View(model);
                }

                model.GuardianName =
                    null;

                model.GuardianEmail =
                    null;

                model.GuardianPhoneNumber =
                    null;

                model.GuardianIdentificationNumber =
                    null;

                HttpContext.Session.SetString(
                    GuardianDetailsSessionKey,
                    JsonSerializer.Serialize(model));

                /*
                 * Continue to Step 3 only after the
                 * Guardian form is submitted successfully.
                 */
                return RedirectToAction(
                    "SelectClasses",
                    "EnrollmentSubmission");
            }

            /*
             * =================================================
             * NEW PARENT / GUARDIAN
             * =================================================
             */

            HttpContext.Session.Remove(
                ExistingParentUserIdSessionKey);

            if (string.IsNullOrWhiteSpace(
                model.GuardianName))
            {
                ModelState.AddModelError(
                    nameof(model.GuardianName),
                    "Please enter the parent or " +
                    "guardian's full name.");
            }

            if (string.IsNullOrWhiteSpace(
                model.GuardianEmail))
            {
                ModelState.AddModelError(
                    nameof(model.GuardianEmail),
                    "Please enter the parent or " +
                    "guardian's email address.");
            }

            if (string.IsNullOrWhiteSpace(
                model.GuardianPhoneNumber))
            {
                ModelState.AddModelError(
                    nameof(model.GuardianPhoneNumber),
                    "Please enter the parent or " +
                    "guardian's phone number.");
            }

            if (string.IsNullOrWhiteSpace(
                model.GuardianIdentificationNumber))
            {
                ModelState.AddModelError(
                    nameof(model.GuardianIdentificationNumber),
                    "Please enter the IC number.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (await GuardianIcValidation.IsRegisteredAsync(
                    _context, model.GuardianIdentificationNumber!, HttpContext.RequestAborted))
            {
                ModelState.AddModelError(nameof(model.GuardianIdentificationNumber),
                    GuardianIcValidation.DuplicateMessage);
                return View(model);
            }

            // Do not silently attach an existing account through the new-account path.
            if (await _userManager.FindByEmailAsync(model.GuardianEmail!.Trim()) != null)
            {
                ModelState.AddModelError(string.Empty,
                    "We couldn't continue with these details. If you already have an EduSmart account, sign in and select the existing account option. Otherwise, contact the centre for assistance.");
                return View(model);
            }

            model.GuardianName =
                model.GuardianName!.Trim();

            model.GuardianEmail =
                model.GuardianEmail!.Trim();

            model.GuardianPhoneNumber =
                model.GuardianPhoneNumber!.Trim();

            model.GuardianIdentificationNumber =
                model.GuardianIdentificationNumber!
                    .Trim();

            model.UseExistingParentAccount =
                false;

            model.ExistingParentVerified =
                false;

            model.ExistingParentEmail =
                null;

            model.ExistingParentIdentificationLast4 =
                null;

            model.VerifiedParentName =
                null;

            model.VerifiedParentEmail =
                null;

            HttpContext.Session.SetString(
                GuardianDetailsSessionKey,
                JsonSerializer.Serialize(model));

            /*
             * Continue to Step 3.
             */
            return RedirectToAction(
                "SelectClasses",
                "EnrollmentSubmission");
        }

        /*
         * =====================================================
         * HELPERS
         * =====================================================
         */
        private bool HasStudentDetails()
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString(
                    StudentDetailsSessionKey));
        }

        private IActionResult ReturnToStudentDetails()
        {
            TempData["EnrollmentMessage"] =
                "Please complete the student details " +
                "before continuing.";

            return RedirectToAction(
                nameof(StudentDetails));
        }
    }
}
