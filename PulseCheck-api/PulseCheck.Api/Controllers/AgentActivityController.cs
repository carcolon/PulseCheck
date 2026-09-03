using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Controllers;

[ApiController]
[EnableRateLimiting("admin-api")]
[Route("api/agent/activity-events")]
public sealed class AgentActivityController(PulseCheckService pulseCheckService) : ControllerBase
{
    [HttpGet("recent")]
    [Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner,WorkforceAdmin")]
    public async Task<ActionResult<IReadOnlyList<AgentActivityEventDto>>> GetRecent(CancellationToken cancellationToken)
    {
        var events = await pulseCheckService.GetRecentAgentActivityEventsAsync(cancellationToken);
        return Ok(events);
    }

    [Authorize(AuthenticationSchemes = AgentTokenAuthenticationDefaults.Scheme)]
    [HttpPost]
    public async Task<IActionResult> Track(
        [FromBody] AgentActivityEventRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.EventType))
        {
            return ValidationProblem("deviceId, userId and eventType are required.");
        }

        if (!RequestMatchesAuthenticatedDevice(request.DeviceId))
        {
            return Forbid();
        }

        var success = await pulseCheckService.TrackAgentActivityAsync(request, cancellationToken);
        return success ? NoContent() : ValidationProblem();
    }

    private bool RequestMatchesAuthenticatedDevice(string deviceId)
    {
        var authenticatedDeviceId = User.FindFirst("device_id")?.Value;
        return !string.IsNullOrWhiteSpace(authenticatedDeviceId) &&
               string.Equals(authenticatedDeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
