using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PulseCheck.Api.Models;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Security;

namespace PulseCheck.Api.Auth;

public sealed class AgentTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IPulseCheckUnitOfWork unitOfWork,
    IOptions<PulseCheckOptions> pulseOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const int MinimumCredentialLifetimeDays = 30;
    private const int MinimumSlidingRenewalDays = 7;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        var credential = await unitOfWork.GetAgentCredentialByTokenHashAsync(
            AgentSecurity.ComputeTokenHash(token),
            Context.RequestAborted);

        var now = DateTimeOffset.UtcNow;
        if (credential is null || credential.RevokedAtUtc is not null || credential.ExpiresAtUtc <= now)
        {
            return AuthenticateResult.Fail("Agent credential not found.");
        }

        var lifetimeDays = Math.Max(pulseOptions.Value.AgentSecurity.CredentialLifetimeDays, MinimumCredentialLifetimeDays);
        var renewalDays = Math.Max(pulseOptions.Value.AgentSecurity.CredentialSlidingRenewalDays, MinimumSlidingRenewalDays);
        credential.LastUsedAtUtc = now;
        if (credential.ExpiresAtUtc <= now.AddDays(renewalDays))
        {
            credential.ExpiresAtUtc = now.AddDays(lifetimeDays);
        }

        await unitOfWork.SaveChangesAsync(Context.RequestAborted);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, credential.DeviceId),
            new Claim("device_id", credential.DeviceId),
            new Claim(ClaimTypes.Role, "Agent")
        };

        var identity = new ClaimsIdentity(claims, AgentTokenAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, AgentTokenAuthenticationDefaults.Scheme));
    }

    private string? ExtractToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var bearerToken = header["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                return bearerToken;
            }
        }

        if (Request.Path.StartsWithSegments("/hubs") &&
            Request.Query.TryGetValue("access_token", out var accessTokenValues))
        {
            var accessToken = accessTokenValues.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                return accessToken;
            }
        }

        return null;
    }
}
