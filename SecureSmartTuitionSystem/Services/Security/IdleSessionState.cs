using Microsoft.Extensions.Caching.Memory;
namespace SecureSmartTuitionSystem.Services.Security;

// Per-worker state is intentionally fail-closed on worker restart. Use a shared,
// atomic store before scaling this application to multiple workers.
public static class IdleSessionState
{
    public const string TicketKey = "EduSmart.IdleSession";
    public static readonly TimeSpan Limit = TimeSpan.FromMinutes(30);
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    public sealed class Entry
    {
        private readonly object _gate = new();
        private DateTimeOffset _last;
        public Entry(DateTimeOffset last) => _last = last;
        public DateTimeOffset Expires { get { lock (_gate) return _last + Limit; } }
        public bool Check(DateTimeOffset now, bool renew)
        {
            lock (_gate)
            {
                if (now >= _last + Limit) return false;
                if (renew && now > _last) _last = now;
                return true;
            }
        }
    }
    public static string Create(DateTimeOffset last)
    {
        var id = Guid.NewGuid().ToString("N");
        Cache.Set(id, new Entry(last), new MemoryCacheEntryOptions { SlidingExpiration = Limit + TimeSpan.FromMinutes(1) });
        return id;
    }
    public static Entry? Find(string id) => Cache.TryGetValue(id, out Entry? entry) ? entry : null;
}
