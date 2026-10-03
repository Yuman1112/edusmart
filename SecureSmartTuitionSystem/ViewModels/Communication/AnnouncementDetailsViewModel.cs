using SecureSmartTuitionSystem.Models.Communication;

namespace SecureSmartTuitionSystem.ViewModels.Communication
{
    public sealed class AnnouncementDetailsViewModel
    {
        public int AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public AnnouncementAudience TargetAudience { get; set; }
        public DateTime PublishAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsArchived { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string DisplayStatus { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public int ReadCount { get; set; }
    }
}
