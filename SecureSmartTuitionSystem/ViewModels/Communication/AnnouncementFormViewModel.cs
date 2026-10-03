using System.ComponentModel.DataAnnotations;
using SecureSmartTuitionSystem.Models.Communication;

namespace SecureSmartTuitionSystem.ViewModels.Communication
{
    public enum AnnouncementExpiryUnit
    {
        None = 0,
        Days = 1,
        Weeks = 2,
        Months = 3
    }

    public sealed class AnnouncementFormViewModel : IValidatableObject
    {
        public int? AnnouncementId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(
            150,
            MinimumLength = 3,
            ErrorMessage = "Title must be between 3 and 150 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Announcement content is required.")]
        [StringLength(
            4000,
            MinimumLength = 5,
            ErrorMessage = "Content must be between 5 and 4000 characters.")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "Target Audience")]
        [EnumDataType(typeof(AnnouncementAudience))]
        public AnnouncementAudience TargetAudience { get; set; }
            = AnnouncementAudience.All;

        [Required(ErrorMessage = "Publish date and time are required.")]
        [Display(Name = "Publish Date and Time")]
        public DateTime PublishAt { get; set; }
            = DateTime.UtcNow.AddHours(8);

        [Display(Name = "Expiry Option")]
        [EnumDataType(typeof(AnnouncementExpiryUnit))]
        public AnnouncementExpiryUnit ExpiryUnit { get; set; }
            = AnnouncementExpiryUnit.None;

        [Display(Name = "Duration")]
        [Range(1, 365, ErrorMessage = "Duration must be between 1 and 365.")]
        public int? ExpiryDuration { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (PublishAt.Year < 2000 || PublishAt.Year > 2100)
                yield return new ValidationResult("Publish date must be between 2000 and 2100 (MYT).", new[] { nameof(PublishAt) });
            if (ExpiryUnit == AnnouncementExpiryUnit.None)
            {
                yield break;
            }

            if (!ExpiryDuration.HasValue || ExpiryDuration.Value < 1)
            {
                yield return new ValidationResult(
                    "Enter an expiry duration of at least 1.",
                    new[] { nameof(ExpiryDuration) });
                yield break;
            }

            int maximum = ExpiryUnit switch
            {
                AnnouncementExpiryUnit.Days => 365,
                AnnouncementExpiryUnit.Weeks => 52,
                AnnouncementExpiryUnit.Months => 12,
                _ => 0
            };

            if (maximum == 0 || ExpiryDuration.Value > maximum)
            {
                yield return new ValidationResult(
                    $"The maximum duration for {ExpiryUnit.ToString().ToLowerInvariant()} is {maximum}.",
                    new[] { nameof(ExpiryDuration) });
            }
        }
    }
}
