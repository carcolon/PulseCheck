using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner,WorkforceAdmin")]
[EnableRateLimiting("admin-api")]
[Route("api/[controller]")]
public sealed class DevicesController(PulseCheckService pulseCheckService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceHeartbeatDto>>> GetDevices(CancellationToken cancellationToken)
    {
        var devices = await pulseCheckService.GetDevicesAsync(cancellationToken);
        return Ok(devices);
    }
}
