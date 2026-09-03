using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Security;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Auth;

public sealed class TransformationalLeaderAuthenticationHandler(
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

        var session = await unitOfWork.GetTransformationalLeaderSessionByTokenHashAsync(AdminSecurity.ComputeTokenHash(token), Context.RequestAborted);
        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return AuthenticateResult.Fail("Session expired.");
        }

        var operations = TransformationalLeaderOperationScope.Parse(session.OperationsJson, session.Operation);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.SolvoId),
            new(ClaimTypes.Email, session.Email),
            new(ClaimTypes.Name, session.DisplayName),
            new(ClaimTypes.Role, "TransformationalLeader"),
            new("tl_session_id", session.Id.ToString("D")),
            new("solvo_id", session.SolvoId),
            new("operation", session.Operation)
        };

        claims.AddRange(operations.Select(operation => new Claim("operations", operation)));

        var identity = new ClaimsIdentity(claims, TransformationalLeaderAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TransformationalLeaderAuthenticationDefaults.Scheme));
    }

    private string? ExtractToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return header["Bearer ".Length..].Trim();
        }

        return null;
    }
}
