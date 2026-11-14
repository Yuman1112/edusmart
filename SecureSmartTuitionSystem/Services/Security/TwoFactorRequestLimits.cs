namespace SecureSmartTuitionSystem.Services.Security;

// Process-wide, independent buckets: page views never consume these quotas.
public static class TwoFactorRequestLimits
{
    private sealed record Bucket(int Count, DateTimeOffset Expires);
    private static readonly object Gate = new();
    private static readonly Dictionary<(string Kind, string Key), Bucket> Buckets = new();
    private static DateTimeOffset nextCleanup;
    private const int MaximumBuckets = 20000;

    // Returns seconds to wait, or zero when the operation is allowed.
    public static int TryAcquire(string userId, string ip, string operation, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        (string Kind, string Key, int Limit, TimeSpan Window)[] rules = operation switch
        {
            "send" => [ ("send-cooldown", userId, 1, TimeSpan.FromSeconds(60)),
                ("send-account", userId, 5, TimeSpan.FromMinutes(15)),
                ("send-ip", ip, 20, TimeSpan.FromMinutes(15)) ],
            "password" => [ ("password-account", userId, 10, TimeSpan.FromMinutes(15)),
                ("password-ip", ip, 50, TimeSpan.FromMinutes(15)) ],
            "verify" => [ ("verify-account", userId, 30, TimeSpan.FromMinutes(1)),
                ("verify-ip", ip, 100, TimeSpan.FromMinutes(1)) ],
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        lock (Gate)
        {
            if (now >= nextCleanup)
            {
                foreach (var key in Buckets.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToArray())
                    Buckets.Remove(key);
                nextCleanup = now.AddMinutes(1);
            }
            int wait = 0;
            foreach (var rule in rules)
                if (Buckets.TryGetValue((rule.Kind, rule.Key), out var state)
                    && state.Expires > now && state.Count >= rule.Limit)
                    wait = Math.Max(wait, (int)Math.Ceiling((state.Expires - now).TotalSeconds));
            if (wait > 0) return wait;
            int newKeys = rules.Count(r => !Buckets.ContainsKey((r.Kind, r.Key)));
            if (Buckets.Count + newKeys > MaximumBuckets) return 60;
            foreach (var rule in rules)
            {
                var key = (rule.Kind, rule.Key);
                var old = Buckets.GetValueOrDefault(key);
                Buckets[key] = old == null || old.Expires <= now
                    ? new Bucket(1, now.Add(rule.Window))
                    : old with { Count = old.Count + 1 };
            }
            return 0;
        }
    }
}
