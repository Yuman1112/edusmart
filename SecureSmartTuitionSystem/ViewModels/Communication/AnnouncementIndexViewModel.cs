using SecureSmartTuitionSystem.Models.Communication;

namespace SecureSmartTuitionSystem.ViewModels.Communication
{
    public sealed class AnnouncementIndexViewModel
    {
        public bool IsAdmin { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
        public AnnouncementAudience? Audience { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<AnnouncementListItemViewModel> Announcements { get; set; }
            = new();
    }

    public sealed class AnnouncementListItemViewModel
    {
        public int AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ContentPreview { get; set; } = string.Empty;
        public AnnouncementAudience TargetAudience { get; set; }
        public DateTime PublishAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsArchived { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string DisplayStatus { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public int ReadCount { get; set; }
    }
}
