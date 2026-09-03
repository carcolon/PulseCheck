using Microsoft.Extensions.Caching.Memory;

namespace PulseCheck.Api.Auth;

public sealed class AdminLoginAttemptLimiter(IMemoryCache cache)
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public bool IsLocked(string email, string? ipAddress)
    {
        var key = BuildKey(email, ipAddress);
        return cache.TryGetValue(key, out LoginAttemptState? state) &&
               state is not null &&
               state.FailedAttempts >= MaxFailedAttempts &&
               state.ExpiresAtUtc > DateTimeOffset.UtcNow;
    }

    public void RecordFailure(string email, string? ipAddress)
    {
        var key = BuildKey(email, ipAddress);
        var state = cache.TryGetValue(key, out LoginAttemptState? existing) && existing is not null
            ? existing with { FailedAttempts = existing.FailedAttempts + 1, ExpiresAtUtc = DateTimeOffset.UtcNow.Add(Window) }
            : new LoginAttemptState(1, DateTimeOffset.UtcNow.Add(Window));

        cache.Set(key, state, state.ExpiresAtUtc);
    }

    public void RecordSuccess(string email, string? ipAddress)
        => cache.Remove(BuildKey(email, ipAddress));

    private static string BuildKey(string email, string? ipAddress)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? "unknown" : email.Trim().ToLowerInvariant();
        var normalizedIp = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress.Trim();
        return $"admin-login:{normalizedEmail}:{normalizedIp}";
    }

    private sealed record LoginAttemptState(int FailedAttempts, DateTimeOffset ExpiresAtUtc);
}
