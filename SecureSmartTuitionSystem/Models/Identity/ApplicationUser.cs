using Microsoft.AspNetCore.Identity;

namespace SecureSmartTuitionSystem.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public bool MustChangePassword { get; set; } = false;
    }
}