using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PulseCheck.Api.Auth;
using PulseCheck.Api.Models;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/auth")]
public sealed class AuthController(
    AdminAuthService adminAuthService,
    TransformationalLeaderAuthService transformationalLeaderAuthService,
    EntraAccessTokenValidator entraAccessTokenValidator,
    EntraAuthorizationCodeFlow entraAuthorizationCodeFlow,
    AdminLoginAttemptLimiter loginAttemptLimiter,
    IOptions<PulseCheckOptions> options) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AdminSessionDto>> Login([FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.AllowLocalAdminLogin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "El login local esta deshabilitado para este entorno." });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (loginAttemptLimiter.IsLocked(request.Email, ipAddress))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Demasiados intentos fallidos. Intenta de nuevo mas tarde." });
        }

        var session = await adminAuthService.LoginAsync(
            request,
            ipAddress,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (session is null)
        {
            loginAttemptLimiter.RecordFailure(request.Email, ipAddress);
            return Unauthorized(new { message = "Credenciales invalidas." });
        }

        loginAttemptLimiter.RecordSuccess(request.Email, ipAddress);
        var csrfToken = AdminSessionCookie.Append(Response, session.Token, session.ExpiresAtUtc);
        return Ok(WithCsrfToken(session, csrfToken));
    }

    [HttpGet("entra/start")]
    public IActionResult StartEntraLogin([FromQuery] string redirectOrigin, [FromQuery] string returnTo = "/admin/overview")
    {
        if (!entraAuthorizationCodeFlow.IsEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "El acceso corporativo no esta configurado." });
        }

        try
        {
            var authorizationUri = entraAuthorizationCodeFlow.CreateAuthorizationUri(Response, redirectOrigin, returnTo);
            return Redirect(authorizationUri.ToString());
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status400BadRequest, new { message = ex.Message });
        }
    }

    [HttpPost("entra/callback")]
    public async Task<ActionResult<AdminSessionDto>> CompleteEntraLogin([FromBody] AdminEntraAuthorizationCodeRequest request, CancellationToken cancellationToken)
    {
        if (!entraAuthorizationCodeFlow.IsEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "El acceso corporativo no esta configurado." });
        }

        var authorizationCode = await entraAuthorizationCodeFlow.RedeemAuthorizationCodeAsync(
            Request,
            Response,
            request.Code,
            request.State,
            request.RedirectUri,
            cancellationToken);

        if (authorizationCode is null)
        {
            return Unauthorized(new { message = "No fue posible completar el inicio de sesion con Microsoft." });
        }

        var validatedPrincipal = await entraAccessTokenValidator.ValidateAccessTokenAsync(authorizationCode.AccessToken, cancellationToken);
        if (validatedPrincipal is null)
        {
            return Unauthorized(new { message = "No fue posible validar el token corporativo." });
        }

        if (string.Equals(authorizationCode.ReturnToPath, "/tl", StringComparison.OrdinalIgnoreCase))
        {
            var tlSession = await transformationalLeaderAuthService.LoginWithEntraAsync(
                validatedPrincipal.Email,
                validatedPrincipal.DisplayName,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken);

            if (tlSession is null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu cuenta corporativa no esta registrada como Transformational Leader activo o no tiene operacion asignada." });
            }

            return Ok(tlSession with { ReturnToPath = authorizationCode.ReturnToPath });
        }

        var session = await adminAuthService.LoginWithEntraAsync(
            validatedPrincipal.Email,
            validatedPrincipal.DisplayName,
            validatedPrincipal.ObjectId,
            validatedPrincipal.TenantId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (session is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu cuenta corporativa no tiene acceso al panel administrativo. Solicita que un owner te agregue como admin." });
        }

        var csrfToken = AdminSessionCookie.Append(Response, session.Token, session.ExpiresAtUtc);
        return Ok(WithCsrfToken(session, csrfToken, authorizationCode.ReturnToPath));
    }

    [HttpPost("entra")]
    public async Task<ActionResult<AdminSessionDto>> EntraLogin([FromBody] AdminEntraLoginRequest request, CancellationToken cancellationToken)
    {
        if (!entraAccessTokenValidator.IsEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "El acceso corporativo no esta configurado." });
        }

        var validatedPrincipal = await entraAccessTokenValidator.ValidateAccessTokenAsync(request.AccessToken, cancellationToken);
        if (validatedPrincipal is null)
        {
            return Unauthorized(new { message = "No fue posible validar el token corporativo." });
        }

        var session = await adminAuthService.LoginWithEntraAsync(
            validatedPrincipal.Email,
            validatedPrincipal.DisplayName,
            validatedPrincipal.ObjectId,
            validatedPrincipal.TenantId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (session is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu cuenta corporativa no tiene acceso al panel administrativo. Solicita que un owner te agregue como admin." });
        }

        var csrfToken = AdminSessionCookie.Append(Response, session.Token, session.ExpiresAtUtc);
        return Ok(WithCsrfToken(session, csrfToken));
    }

    [Authorize(AuthenticationSchemes = $"{AdminTokenAuthenticationDefaults.Scheme},{TransformationalLeaderAuthenticationDefaults.Scheme}")]
    [HttpGet("session")]
    public async Task<ActionResult<object>> GetSession(CancellationToken cancellationToken)
    {
        if (User.IsInRole("TransformationalLeader"))
        {
            var tlSession = await transformationalLeaderAuthService.GetSessionAsync(ExtractAdminToken(), cancellationToken);
            return tlSession is null ? Unauthorized() : Ok(tlSession);
        }

        var session = await adminAuthService.GetSessionAsync(ExtractAdminToken(), cancellationToken);
        if (session is null)
        {
            return Unauthorized();
        }

        if (Request.Cookies.ContainsKey(AdminSessionCookie.Name) && !HasBearerToken())
        {
            var csrfToken = AdminSessionCookie.Append(Response, ExtractAdminToken(), session.ExpiresAtUtc);
            return Ok(WithCsrfToken(session, csrfToken));
        }

        return Ok(session);
    }

    [Authorize(AuthenticationSchemes = $"{AdminTokenAuthenticationDefaults.Scheme},{TransformationalLeaderAuthenticationDefaults.Scheme}")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (User.IsInRole("TransformationalLeader"))
        {
            await transformationalLeaderAuthService.LogoutAsync(ExtractAdminToken(), cancellationToken);
            return NoContent();
        }

        await adminAuthService.LogoutAsync(ExtractAdminToken(), cancellationToken);
        AdminSessionCookie.Delete(Response);
        return NoContent();
    }

    private string ExtractAdminToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return header["Bearer ".Length..].Trim();
        }

        return Request.Cookies.TryGetValue(AdminSessionCookie.Name, out var cookieToken)
            ? cookieToken.Trim()
            : string.Empty;
    }

    private bool HasBearerToken()
        => Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

    private static AdminSessionDto WithCsrfToken(AdminSessionDto session, string csrfToken)
        => session with { CsrfToken = csrfToken };

    private static AdminSessionDto WithCsrfToken(AdminSessionDto session, string csrfToken, string returnToPath)
        => session with { CsrfToken = csrfToken, ReturnToPath = returnToPath };
}
