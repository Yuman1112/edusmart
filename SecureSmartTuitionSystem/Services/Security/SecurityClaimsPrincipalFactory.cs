using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Services.Security;

public sealed class SecurityClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public SecurityClaimsPrincipalFactory(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles,
        IOptions<IdentityOptions> options) : base(users, roles, options) { }
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("EduSmart.LastActivityUtc",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        return identity;
    }
}
