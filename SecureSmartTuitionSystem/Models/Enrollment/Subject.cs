using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public enum EducationStage
    {
        Primary,
        Secondary
    }

    public class Subject
    {
        public int SubjectId { get; set; }

        [Required]
        [Display(Name = "Education Stage")]
        public EducationStage EducationStage { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "Academic Level")]
        public string AcademicLevel { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Subject Name")]
        public string SubjectName { get; set; } = string.Empty;

        [Range(
            0,
            99999,
            ErrorMessage =
                "Monthly fee must be between RM0 and RM99,999.")]
        [Display(Name = "Monthly Fee")]
        public decimal MonthlyFee { get; set; } = 0;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public ICollection<ClassSession> ClassSessions { get; set; }
            = new List<ClassSession>();
    }
}