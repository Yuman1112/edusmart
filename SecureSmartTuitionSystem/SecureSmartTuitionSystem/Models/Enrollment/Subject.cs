using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public class Subject
    {
        public int SubjectId { get; set; }

        [Required]
        [StringLength(100)]
        public string SubjectName { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string AcademicLevel { get; set; } = string.Empty;

        [Range(0, 99999)]
        public decimal MonthlyFee { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<ClassSession> ClassSessions { get; set; }
            = new List<ClassSession>();
    }
}