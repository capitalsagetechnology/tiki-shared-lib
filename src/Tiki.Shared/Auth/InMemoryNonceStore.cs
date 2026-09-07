using System.Collections.Concurrent;

namespace Tiki.Shared.Auth;

/// <summary>
/// Process-local <see cref="INonceStore"/> for single-instance deployments and tests.
///
/// <para>
/// Correct only while a service runs as one instance: two replicas each keep their own set,
/// so a request replayed against the other replica is not recognised as a repeat.
/// <see cref="RedisNonceStore"/> is the multi-instance answer — this one is what
/// <c>AddTikiServiceAuth</c> falls back to when no Redis is configured, so a service still
/// gets replay protection rather than silently getting none.
/// </para>
/// </summary>
public sealed class InMemoryNonceStore : INonceStore, IDisposable
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly Timer _sweeper;

    public InMemoryNonceStore()
    {
        // Without this the dictionary grows for the lifetime of the process. Sweeping on a
        // timer rather than on every call keeps the hot path a single dictionary write.
        _sweeper = new Timer(_ => Sweep(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public Task<bool> TryConsumeAsync(string serviceId, string nonce, TimeSpan retention, CancellationToken ct = default)
    {
        var key = $"{serviceId}:{nonce}";
        var expiresAt = DateTimeOffset.UtcNow.Add(retention);

        // AddOrUpdate runs atomically per key, so exactly one of two concurrent callers can
        // observe the slot as free. Doing this as TryAdd-then-fix-up would reintroduce the
        // race the interface exists to prevent.
        var wasNew = false;
        _seen.AddOrUpdate(
            key,
            _ =>
            {
                wasNew = true;
                return expiresAt;
            },
            (_, existing) =>
            {
                // An entry that aged out but has not been swept yet must not reject a fresh
                // nonce — vanishingly unlikely at 128 bits, but free to handle correctly.
                if (existing > DateTimeOffset.UtcNow)
                    return existing;

                wasNew = true;
                return expiresAt;
            });

        return Task.FromResult(wasNew);
    }

    private void Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, expiresAt) in _seen)
        {
            if (expiresAt <= now)
                _seen.TryRemove(key, out _);
        }
    }

    public void Dispose() => _sweeper.Dispose();
}
