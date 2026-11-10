using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.ViewModels.Identity;

public sealed class AccountProfileModel
{
    public string Role { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? Email { get; set; }
    public string? Detail { get; set; }

    [Required, StringLength(100)]
    public string FullName { get; set; } = "";

    [StringLength(150)]
    public string? School { get; set; }

    [Phone, StringLength(30)]
    public string? PhoneNumber { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }
}
