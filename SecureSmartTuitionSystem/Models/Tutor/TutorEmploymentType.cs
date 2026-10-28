using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.Models.Tutor;
public enum TutorEmploymentType
{
    [Display(Name = "Part-time")] PartTime = 0,
    [Display(Name = "Full-time")] FullTime = 1
}
