using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Services.Authentication
{
    public class AccountProvisioningService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountProvisioningService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<string> GenerateNextUserIdAsync(
            string prefix)
        {
            var userNames = await _context.Users
                .Where(user =>
                    user.UserName != null &&
                    user.UserName.StartsWith(prefix))
                .Select(user => user.UserName!)
                .ToListAsync();

            var maxNumber = 0;

            foreach (var userName in userNames)
            {
                var numericPart =
                    userName.Substring(prefix.Length);

                if (int.TryParse(numericPart, out var number) &&
                    number > maxNumber)
                {
                    maxNumber = number;
                }
            }

            return $"{prefix}{maxNumber + 1:D5}";
        }

        public async Task<ApplicationUser> CreateStudentAccountAsync(
            string fullName,
            string email,
            string temporaryPassword)
        {
            var existingEmail =
                await _userManager.FindByEmailAsync(email);

            if (existingEmail != null)
            {
                throw new InvalidOperationException(
                    "This email address is already registered.");
            }

            var studentUserId =
                await GenerateNextUserIdAsync("ST");

            var studentUser = new ApplicationUser
            {
                UserName = studentUserId,
                Email = email,
                FullName = fullName,
                IsActive = true,
                LockoutEnabled = true,
                EmailConfirmed = false,
                MustChangePassword = false,
                TwoFactorEnabled = false
            };

            var createResult =
                await _userManager.CreateAsync(
                    studentUser,
                    temporaryPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    createResult.Errors
                        .Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Unable to create student account: {errors}");
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    studentUser,
                    "Student");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(studentUser);

                var errors = string.Join(
                    ", ",
                    roleResult.Errors
                        .Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Unable to assign Student role: {errors}");
            }

            return studentUser;
        }

        public async Task<ApplicationUser> CreateParentAccountAsync(
            string fullName,
            string email,
            string phoneNumber,
            string temporaryPassword)
        {
            var existingEmail =
                await _userManager.FindByEmailAsync(email);

            if (existingEmail != null)
            {
                throw new InvalidOperationException(
                    "This email address is already registered.");
            }

            var parentUserId =
                await GenerateNextUserIdAsync("PA");

            var parentUser = new ApplicationUser
            {
                UserName = parentUserId,
                Email = email,
                PhoneNumber = phoneNumber,
                FullName = fullName,
                IsActive = true,
                LockoutEnabled = true,
                EmailConfirmed = false,
                MustChangePassword = false,
                TwoFactorEnabled = false
            };

            var createResult =
                await _userManager.CreateAsync(
                    parentUser,
                    temporaryPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    createResult.Errors
                        .Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Unable to create parent account: {errors}");
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    parentUser,
                    "Parent");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(parentUser);

                var errors = string.Join(
                    ", ",
                    roleResult.Errors
                        .Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Unable to assign Parent role: {errors}");
            }

            return parentUser;
        }
    }
}