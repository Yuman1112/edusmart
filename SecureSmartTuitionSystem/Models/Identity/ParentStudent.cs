using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models.Identity
{
    public class ParentStudent
    {
        public int ParentStudentId { get; set; }

        public int ParentProfileId { get; set; }

        public int StudentProfileId { get; set; }

        [StringLength(50)]
        public string? Relationship { get; set; }

        public bool IsPrimaryGuardian { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

        public DateTime? EndedAt { get; set; }

        public ParentProfile ParentProfile { get; set; } = null!;

        public StudentProfile StudentProfile { get; set; } = null!;
    }
}