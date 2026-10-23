
using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.Models
{
    public class TutorTeachingSubject
    {
        public int TutorTeachingSubjectId { get; set; }

        [Required]
        public int TutorProfileId { get; set; }

        [Required]
        [StringLength(20)]
        public string EducationStage { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string AcademicLevel { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string SubjectCode { get; set; } = string.Empty;

        public TutorProfile TutorProfile { get; set; } = null!;
    }
}
