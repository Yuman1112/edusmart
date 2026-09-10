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
    }
}