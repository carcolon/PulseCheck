using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = TransformationalLeaderAuthenticationDefaults.Scheme, Roles = "TransformationalLeader")]
[EnableRateLimiting("admin-api")]
[Route("api/tl/dashboard")]
public sealed class TlDashboardController(
    TransformationalLeaderAuthService transformationalLeaderAuthService,
    TransformationalLeaderDashboardService transformationalLeaderDashboardService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TlDashboardDto>> GetDashboard(
        [FromBody] TlDashboardRequest request,
        CancellationToken cancellationToken)
    {
        var session = await transformationalLeaderAuthService.GetSessionAsync(ExtractToken(), cancellationToken);
        if (session is null)
        {
            return Unauthorized();
        }

        var dashboard = await transformationalLeaderDashboardService.GetDashboardAsync(session, request, cancellationToken);
        return Ok(dashboard);
    }

    private string ExtractToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
    }
}
