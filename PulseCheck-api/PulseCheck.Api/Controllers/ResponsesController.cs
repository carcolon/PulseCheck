using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Controllers;

[ApiController]
[EnableRateLimiting("admin-api")]
[Route("api/[controller]")]
public sealed class ResponsesController(PulseCheckService pulseCheckService) : ControllerBase
{
    [Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner,HRAdmin")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PulseResponseDto>>> GetResponses(CancellationToken cancellationToken)
    {
        var responses = await pulseCheckService.GetResponsesAsync(cancellationToken);
        return Ok(responses);
    }

    [Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner,HRAdmin")]
    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<PulseResponseDto>>> GetRecentResponses(CancellationToken cancellationToken)
    {
        var responses = await pulseCheckService.GetRecentResponsesAsync(cancellationToken);
        return Ok(responses);
    }

    [Authorize(AuthenticationSchemes = AgentTokenAuthenticationDefaults.Scheme)]
    [HttpPost]
    public async Task<ActionResult<PulseResponseDto>> SubmitResponse([FromBody] SubmitResponseRequest request, CancellationToken cancellationToken)
    {
        if (!RequestMatchesAuthenticatedDevice(request.DeviceId))
        {
            return Forbid();
        }

        var response = await pulseCheckService.SubmitResponseAsync(request, cancellationToken);
        if (response is null)
        {
            return ValidationProblem("Respuesta invalida. Verifica tipo de pregunta, rango o contenido y que la campaña exista.");
        }

        return Accepted(response);
    }

    private bool RequestMatchesAuthenticatedDevice(string deviceId)
    {
        var authenticatedDeviceId = User.FindFirst("device_id")?.Value;
        return !string.IsNullOrWhiteSpace(authenticatedDeviceId) &&
               string.Equals(authenticatedDeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
