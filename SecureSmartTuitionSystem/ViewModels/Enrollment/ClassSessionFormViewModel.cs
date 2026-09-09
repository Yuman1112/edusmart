using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SecureSmartTuitionSystem.Models.Enrollment;

namespace SecureSmartTuitionSystem.ViewModels.Enrollment
{
    public class ClassSessionFormViewModel
    {
        public int? ClassSessionId { get; set; }

        [Required(ErrorMessage = "Please select an education stage.")]
        [Display(Name = "Education Stage")]
        public EducationStage? EducationStage { get; set; }

        [Required(ErrorMessage = "Please select an academic level.")]
        [Display(Name = "Academic Level")]
        public string AcademicLevel { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a subject.")]
        [Display(Name = "Subject Name")]
        public string SubjectName { get; set; } = string.Empty;

        [Range(
            0,
            99999,
            ErrorMessage =
                "Monthly fee must be between RM0 and RM99,999.")]
        [Display(Name = "Monthly Fee")]
        public decimal MonthlyFee { get; set; } = 0;

        [StringLength(
            200,
            ErrorMessage = "Class name cannot exceed 200 characters.")]
        [Display(Name = "Class Name")]
        public string? ClassName { get; set; }

        [Required(ErrorMessage = "Please select a tutor.")]
        [Display(Name = "Tutor")]
        public string TutorUserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a class day.")]
        [Display(Name = "Class Day")]
        public DayOfWeek? DayOfWeek { get; set; }

        [Required(ErrorMessage = "Start time is required.")]
        [DataType(DataType.Time)]
        [Display(Name = "Start Time")]
        public TimeOnly? StartTime { get; set; }

        [Required(ErrorMessage = "End time is required.")]
        [DataType(DataType.Time)]
        [Display(Name = "End Time")]
        public TimeOnly? EndTime { get; set; }

        [Required(ErrorMessage = "Google Meet link is required.")]
        [Url(ErrorMessage = "Please enter a valid URL.")]
        [StringLength(100)]
        [RegularExpression(
            @"^https:\/\/meet\.google\.com\/[a-zA-Z0-9\-]+\/?$",
            ErrorMessage =
                "Please enter a valid Google Meet link, for example: https://meet.google.com/abc-defg-hij")]
        [Display(Name = "Google Meet Link")]
        public string Venue { get; set; } = string.Empty;

        [Required(ErrorMessage = "Capacity is required.")]
        [Range(
            1,
            100,
            ErrorMessage = "Capacity must be between 1 and 100.")]
        public int Capacity { get; set; } = 20;

        public List<SelectListItem> EducationStages { get; set; }
            = new();

        public List<SelectListItem> Tutors { get; set; }
            = new();

        public List<SelectListItem> Days { get; set; }
            = new();
    }
}