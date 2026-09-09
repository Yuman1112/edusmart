using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Enrollment;

namespace SecureSmartTuitionSystem.Controllers
{
    [AllowAnonymous]
    public class EnrollmentController : Controller
    {
        private const string StudentDetailsSessionKey = "Enrollment.StudentDetails";
        private const string GuardianDetailsSessionKey = "Enrollment.GuardianDetails";
        private const string ExistingParentUserIdSessionKey = "Enrollment.ExistingParentUserId";
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public EnrollmentController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // =========================================================
        // START NEW ENROLLMENT
        // =========================================================
        [HttpGet]
        public IActionResult Start()
        {
            // Clear any previous enrollment session data.
            HttpContext.Session.Remove(StudentDetailsSessionKey);
            HttpContext.Session.Remove(GuardianDetailsSessionKey);
            HttpContext.Session.Remove(ExistingParentUserIdSessionKey);

            return RedirectToAction(nameof(StudentDetails));
        }

        // =========================================================
        // STEP 1 - STUDENT DETAILS
        // =========================================================
        [HttpGet]
        public IActionResult StudentDetails()
        {
            var json = HttpContext.Session.GetString(
                StudentDetailsSessionKey);

            var model = string.IsNullOrEmpty(json)
                ? new StudentDetailsViewModel()
                : JsonSerializer.Deserialize<StudentDetailsViewModel>(json)
                    ?? new StudentDetailsViewModel();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult StudentDetails(
            StudentDetailsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.StudentName = model.StudentName.Trim();
            model.StudentIdentificationType = model.StudentIdentificationType.Trim();
            model.StudentIdentificationNumber = model.StudentIdentificationNumber.Trim();
            model.StudentSchool = model.StudentSchool.Trim();

            // Save student details before opening the guardian page.
            HttpContext.Session.SetString(
                StudentDetailsSessionKey,
                JsonSerializer.Serialize(model));

            return RedirectToAction(nameof(GuardianDetails));
        }

        // =========================================================
        // STEP 2 - GUARDIAN DETAILS
        // =========================================================
        [HttpGet]
        public IActionResult GuardianDetails()
        {
            if (!HasStudentDetails())
            {
                return ReturnToStudentDetails();
            }

            var json = HttpContext.Session.GetString(GuardianDetailsSessionKey);

            var model = string.IsNullOrEmpty(json)
                ? new GuardianDetailsViewModel()
                : JsonSerializer.Deserialize<GuardianDetailsViewModel>(json)
                    ?? new GuardianDetailsViewModel();

            // If the verification Session no longer exists,
            // do not show the account as verified.
            if (model.UseExistingParentAccount)
            {
                var existingParentUserId =
                    HttpContext.Session.GetString(
                        ExistingParentUserIdSessionKey);

                if (string.IsNullOrWhiteSpace(
                        existingParentUserId))
                {
                    model.ExistingParentVerified = false;
                    model.VerifiedParentName = null;
                    model.VerifiedParentEmail = null;
                }
            }

            return View(model);
        }

        // =========================================================
        // VERIFY EXISTING PARENT ACCOUNT
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyExistingParent(GuardianDetailsViewModel model)
        {
            if (!HasStudentDetails())
            {
                return ReturnToStudentDetails();
            }

            model.UseExistingParentAccount = true;

            // Remove previous verification first.
            HttpContext.Session.Remove(ExistingParentUserIdSessionKey);

            model.ExistingParentVerified = false;
            model.VerifiedParentName = null;
            model.VerifiedParentEmail = null;

            // -----------------------------------------------------
            // Required validation for existing Parent
            // -----------------------------------------------------
            if (string.IsNullOrWhiteSpace(model.ExistingParentEmail))
            {
                ModelState.AddModelError(
                    nameof(model.ExistingParentEmail),
                    "Please enter the email registered with your account.");
            }

            if (string.IsNullOrWhiteSpace(model.ExistingParentIdentificationLast4))
            {
                ModelState.AddModelError(
                    nameof(model.ExistingParentIdentificationLast4),
                    "Please enter the last 4 digits of your identification number.");
            }

            if (!ModelState.IsValid)
            {
                return View(
                    "GuardianDetails",
                    model);
            }

            var email = model.ExistingParentEmail!.Trim();
            var identificationLast4 = model.ExistingParentIdentificationLast4!.Trim();
            var normalizedEmail = _userManager.NormalizeEmail(email);

            // -----------------------------------------------------
            // Find ParentProfile using: Email + IC Last 4
            // -----------------------------------------------------
            var parentProfile =
                await _context.ParentProfiles
                    .Include(p => p.ApplicationUser)
                    .FirstOrDefaultAsync(p =>
                        p.ApplicationUser.NormalizedEmail ==
                            normalizedEmail &&
                        p.IdentificationLast4 ==
                            identificationLast4);

            // Use the same generic error for security.
            if (parentProfile == null ||
                parentProfile.ApplicationUser == null ||
                !parentProfile.ApplicationUser.IsActive)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "We couldn't find an active parent / guardian " +
                    "account matching those details. Please check " +
                    "the email and identification number.");

                return View(
                    "GuardianDetails",
                    model);
            }

            var isParent =
                await _userManager.IsInRoleAsync(
                    parentProfile.ApplicationUser,
                    "Parent");

            if (!isParent)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "We couldn't find an active parent / guardian " +
                    "account matching those details. Please check " +
                    "the email and identification number.");

                return View(
                    "GuardianDetails",
                    model);
            }

            // -----------------------------------------------------
            // Verification successful
            // -----------------------------------------------------
            model.ExistingParentVerified = true;
            model.VerifiedParentName = parentProfile.FullName;
            model.VerifiedParentEmail = parentProfile.ApplicationUser.Email;

            // Store the actual Parent user ID in Session.
            HttpContext.Session.SetString(
                ExistingParentUserIdSessionKey,
                parentProfile.ApplicationUserId);

            // Save verified Guardian Step 2 information.
            HttpContext.Session.SetString(
                GuardianDetailsSessionKey,
                JsonSerializer.Serialize(model));

            TempData["SuccessMessage"] =
                "Account verified — this student will be linked " +
                "to your existing parent / guardian account.";

            return RedirectToAction(
                nameof(GuardianDetails));
        }

        // =========================================================
        // STEP 2 - BACK TO STUDENT DETAILS
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BackToStudentDetails(
            GuardianDetailsViewModel model)
        {
            if (!HasStudentDetails())
            {
                return ReturnToStudentDetails();
            }

            // =====================================================
            // EXISTING PARENT
            // =====================================================
            if (model.UseExistingParentAccount)
            {
                var existingParentUserId =
                    HttpContext.Session.GetString(
                        ExistingParentUserIdSessionKey);

                var savedJson =
                    HttpContext.Session.GetString(
                        GuardianDetailsSessionKey);

                GuardianDetailsViewModel? savedModel = null;

                if (!string.IsNullOrWhiteSpace(savedJson))
                {
                    savedModel =
                        JsonSerializer.Deserialize
                            <GuardianDetailsViewModel>(savedJson);
                }

                // Preserve verified status only from server-side Session.
                if (!string.IsNullOrWhiteSpace(existingParentUserId) &&
                    savedModel != null &&
                    savedModel.ExistingParentVerified)
                {
                    model.ExistingParentVerified = true;

                    model.VerifiedParentName =
                        savedModel.VerifiedParentName;

                    model.VerifiedParentEmail =
                        savedModel.VerifiedParentEmail;

                    model.ExistingParentEmail =
                        savedModel.ExistingParentEmail;

                    model.ExistingParentIdentificationLast4 =
                        savedModel.ExistingParentIdentificationLast4;
                }
                else
                {
                    model.ExistingParentVerified = false;
                    model.VerifiedParentName = null;
                    model.VerifiedParentEmail = null;
                }

                // Clear New Parent fields.
                model.GuardianName = null;
                model.GuardianEmail = null;
                model.GuardianPhoneNumber = null;
                model.GuardianIdentificationNumber = null;
            }
            else
            {
                // New Parent / Guardian.
                HttpContext.Session.Remove(
                    ExistingParentUserIdSessionKey);

                model.ExistingParentVerified = false;
                model.ExistingParentEmail = null;
                model.ExistingParentIdentificationLast4 = null;
                model.VerifiedParentName = null;
                model.VerifiedParentEmail = null;

                // Keep entered draft data.
                model.GuardianName =
                    model.GuardianName?.Trim();

                model.GuardianEmail =
                    model.GuardianEmail?.Trim();

                model.GuardianPhoneNumber =
                    model.GuardianPhoneNumber?.Trim();

                model.GuardianIdentificationNumber =
                    model.GuardianIdentificationNumber?.Trim();
            }

            // Save Step 2 draft into Session.
            HttpContext.Session.SetString(
                GuardianDetailsSessionKey,
                JsonSerializer.Serialize(model));

            return RedirectToAction(nameof(StudentDetails));
        }

        // =========================================================
        // STEP 2 - CONTINUE
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuardianDetails(
            GuardianDetailsViewModel model)
        {
            if (!HasStudentDetails())
            {
                return ReturnToStudentDetails();
            }

            // =====================================================
            // EXISTING PARENT
            // =====================================================
            if (model.UseExistingParentAccount)
            {
                var existingParentUserId =
                    HttpContext.Session.GetString(
                        ExistingParentUserIdSessionKey);

                // Parent must click Verify Account first.
                if (string.IsNullOrWhiteSpace(existingParentUserId))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Please verify your existing parent / guardian " +
                        "account before continuing.");

                    model.ExistingParentVerified = false;

                    return View(model);
                }

                // Restore verified details from Session instead of trusting hidden form values.
                var savedJson =
                    HttpContext.Session.GetString(
                        GuardianDetailsSessionKey);

                if (!string.IsNullOrWhiteSpace(savedJson))
                {
                    var savedModel =
                        JsonSerializer.Deserialize
                            <GuardianDetailsViewModel>(
                                savedJson);

                    if (savedModel != null)
                    {
                        model.ExistingParentEmail = savedModel.ExistingParentEmail;
                        model.ExistingParentIdentificationLast4 = savedModel.ExistingParentIdentificationLast4;
                        model.ExistingParentVerified = true;
                        model.VerifiedParentName = savedModel.VerifiedParentName;
                        model.VerifiedParentEmail = savedModel.VerifiedParentEmail;
                    }
                }

                HttpContext.Session.SetString(
                    GuardianDetailsSessionKey,
                    JsonSerializer.Serialize(model));

                TempData["SuccessMessage"] =
                    "Existing parent / guardian account selected. " +
                    "The enrollment has not been submitted yet.";

                // Step 3 is not connected yet.
                // Change this to Step 3 later.
                return RedirectToAction(
                    nameof(GuardianDetails)); }

            // =====================================================
            // NEW PARENT / GUARDIAN
            // =====================================================
            // Clear any previous existing Parent verification.
            HttpContext.Session.Remove(
                ExistingParentUserIdSessionKey);

            // -----------------------------------------------------
            // Conditional Required Validation
            // -----------------------------------------------------
            if (string.IsNullOrWhiteSpace(model.GuardianName))
            {
                ModelState.AddModelError(
                    nameof(model.GuardianName),
                    "Please enter the parent or guardian's name.");
            }

            if (string.IsNullOrWhiteSpace(model.GuardianEmail))
            {
                ModelState.AddModelError(
                    nameof(model.GuardianEmail),
                    "Please enter an email address.");
            }

            if (string.IsNullOrWhiteSpace(model.GuardianPhoneNumber))
            {
                ModelState.AddModelError(
                    nameof(model.GuardianPhoneNumber),
                    "Please enter a phone number.");
            }

            if (string.IsNullOrWhiteSpace(
                    model.GuardianIdentificationNumber))
            {
                ModelState.AddModelError(
                    nameof(model.GuardianIdentificationNumber),
                    "Please enter the parent or guardian's identification number.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // -----------------------------------------------------
            // Trim / normalize values
            // -----------------------------------------------------
            model.GuardianName = model.GuardianName!.Trim();
            model.GuardianEmail = model.GuardianEmail!.Trim();
            model.GuardianPhoneNumber = model.GuardianPhoneNumber!.Trim();
            model.GuardianIdentificationNumber = model.GuardianIdentificationNumber!.Trim();

            model.UseExistingParentAccount = false;
            model.ExistingParentVerified = false;
            model.ExistingParentEmail = null;
            model.ExistingParentIdentificationLast4 = null;

            model.VerifiedParentName = null;
            model.VerifiedParentEmail = null;

            // Store new guardian details only.
            // Account is NOT created yet.
            HttpContext.Session.SetString(
                GuardianDetailsSessionKey,
                JsonSerializer.Serialize(model));

            TempData["SuccessMessage"] =
                "Student and guardian details saved temporarily. " +
                "Your enrollment has not been submitted yet.";

            // Step 3 will be connected to teammate's flow later.
            return RedirectToAction(nameof(GuardianDetails));
        }

        // =========================================================
        // HELPERS
        // =========================================================
        private bool HasStudentDetails()
        {
            return !string.IsNullOrEmpty(
                HttpContext.Session.GetString(
                    StudentDetailsSessionKey));
        }

        private IActionResult ReturnToStudentDetails()
        {
            TempData["EnrollmentMessage"] =
                "Please complete the student details before continuing.";

            return RedirectToAction(nameof(StudentDetails));
        }
    }
}