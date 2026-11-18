namespace SecureSmartTuitionSystem.Services.Security;

public sealed record AccountRequestCooldown(int Password, int Send, int Verify)
{
    public static AccountRequestCooldown Read(string userKey, string ip)
    {
        var now = DateTimeOffset.UtcNow;
        return new(TwoFactorRequestLimits.GetWaitSeconds(userKey, ip, "password", now),
            TwoFactorRequestLimits.GetWaitSeconds(userKey, ip, "send", now),
            TwoFactorRequestLimits.GetWaitSeconds(userKey, ip, "verify", now));
    }
}
