using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SecureSmartTuitionSystem.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public bool MustChangePassword { get; set; } = false;

        // The new tutor email awaiting verification.
        [StringLength(254)]
        public string? PendingTutorEmail { get; set; }

        // Identifies the current request so replaced or cancelled
        // requests can be rejected.
        public Guid? TutorEmailChangeRequestId { get; set; }

        public DateTime? TutorEmailChangeRequestedAtUtc { get; set; }

        public DateTime? TutorEmailChangeExpiresAtUtc { get; set; }

        // The Admin who requested the email change.
        [StringLength(450)]
        public string? TutorEmailChangeRequestedByUserId { get; set; }
    }
}