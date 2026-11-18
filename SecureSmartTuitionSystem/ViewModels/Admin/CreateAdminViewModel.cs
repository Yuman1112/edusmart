
using System.ComponentModel.DataAnnotations;

namespace SecureSmartTuitionSystem.ViewModels.Admin
{
    public sealed class CreateAdminViewModel
    {
        [Required(ErrorMessage = "Please enter the full name.")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter an email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(254)]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        private string? _phoneNumber;

        [RegularExpression(@"^01[0-9]-[0-9]{7,8}$", ErrorMessage = "Please enter a valid Malaysian phone number.")]
        [StringLength(12, ErrorMessage = "Please enter a valid Malaysian phone number.")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) { _phoneNumber = null; return; }
                var text = System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"[\s()-]", "");
                if (text.StartsWith("+60", StringComparison.Ordinal)) text = "0" + text[3..];
                else if (text.StartsWith("60", StringComparison.Ordinal)) text = "0" + text[2..];
                // Do not truncate or strip letters on the server: reject invalid submissions.
                _phoneNumber = System.Text.RegularExpressions.Regex.IsMatch(text, @"^[0-9]+$") && text.Length > 3
                    ? text[..3] + "-" + text[3..] : text;
            }
        }

        [StringLength(100)]
        [Display(Name = "Position")]
        public string? Position { get; set; }
    }
}
