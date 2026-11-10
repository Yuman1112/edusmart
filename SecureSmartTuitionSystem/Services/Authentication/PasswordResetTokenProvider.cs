using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Services.Authentication;

public sealed class PasswordResetTokenOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenOptions()
    {
        Name = "EduSmart.PasswordReset";
        TokenLifespan = TimeSpan.FromMinutes(30);
    }
}

public sealed class PasswordResetTokenProvider(
    IDataProtectionProvider protection,
    IOptions<PasswordResetTokenOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(protection, options, logger)
{
    public const string ProviderName = "EduSmartPasswordReset";
}

