using System.ComponentModel.DataAnnotations;
namespace SecureSmartTuitionSystem.ViewModels.Communication;
public sealed class GuestSupportForm : IValidatableObject
{
    public const string EmailPattern = @"^(?=.{1,254}$)(?=[^@]{1,64}@)(?![^@]*\.\.)[A-Za-z0-9!#$%&'*+/=?^_`{|}~-](?:[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]*[A-Za-z0-9!#$%&'*+/=?^_`{|}~-])?@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$";
    [Required(ErrorMessage = "Name field is required."), StringLength(100),
     RegularExpression(@"[A-Za-z /-]+", ErrorMessage = "Name can only contain A-Z, a-z, spaces, / and -.")] public string Name { get; set; } = "";
    [Required(ErrorMessage = "Email field is required."), EmailAddress(ErrorMessage = "Please enter a valid email address."), StringLength(254),
     RegularExpression(EmailPattern, ErrorMessage = "Please enter a valid email address.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Message field is required."), StringLength(2000, MinimumLength = 5)] public string Message { get; set; } = "";
    public bool Consent { get; set; }
    public Guid SubmissionKey { get; set; }
    [StringLength(100)] public string? Website { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!Consent) yield return new ValidationResult("This field is required.", new[] { nameof(Consent) });
    }
}
public sealed record GuestContact(string Name, string Email, string Message);
public sealed record GuestEnquiryRow(int Id, DateTime CreatedAtUtc, bool IsResolved, GuestContact Contact,
    DateTime? ReadAtUtc = null, Models.Communication.GuestReplyState ReplyState = Models.Communication.GuestReplyState.None,
    DateTime? ReplySentAtUtc = null, string ReplyText = "", Guid? ReplyAttemptId = null, DateTime? ReplyClaimedAtUtc = null, string RecoveryHistory = "", IReadOnlyList<GuestEnquiryTurn>? Conversation = null)
{
    public string Status => IsResolved ? "Resolved" : ReadAtUtc.HasValue ? "Awaiting reply" : "Unread";
}
public sealed record GuestInbox(List<GuestEnquiryRow> Rows, string Status, int Page, bool HasNext,
    int Total, int Unread, int AwaitingReply, int Resolved)
{
    public int FilteredTotal => Status switch { "unread" => Unread, "awaiting" => AwaitingReply, "resolved" => Resolved, _ => Total };
    public int TotalPages => Math.Max(1, (FilteredTotal + 24) / 25);
}
public sealed class GuestReplyForm
{
    public int ConversationVersion { get; set; }
    public int Id { get; set; }
    [Required(ErrorMessage = "Reply field is required."), StringLength(4000, MinimumLength = 5)]
    public string Reply { get; set; } = "";
}
public sealed record GuestEnquiryDetails(GuestEnquiryRow Enquiry, GuestReplyForm Reply);

public sealed record GuestEnquiryTurn(string Question, string Reply, DateTime? SentAtUtc, DateTime ReceivedAtUtc, DateTime RecordedAtUtc, string RecordedBy);
