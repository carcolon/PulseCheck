using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Security;

namespace PulseCheck.Api.Auth;

public sealed class AdminTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IPulseCheckUnitOfWork unitOfWork)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        var session = await unitOfWork.GetAdminSessionByTokenHashAsync(AdminSecurity.ComputeTokenHash(token), Context.RequestAborted);
        if (session?.AdminUser is null)
        {
            return AuthenticateResult.Fail("Session not found.");
        }

        if (!session.AdminUser.IsActive || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return AuthenticateResult.Fail("Session expired.");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, session.AdminUser.Id.ToString()),
            new Claim(ClaimTypes.Email, session.AdminUser.Email),
            new Claim(ClaimTypes.Name, session.AdminUser.DisplayName)
        };

        foreach (var role in AdminRoles.Parse(session.AdminUser.Role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, AdminTokenAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, AdminTokenAuthenticationDefaults.Scheme));
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

        if (Request.Cookies.TryGetValue(AdminSessionCookie.Name, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            return cookieToken.Trim();
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
