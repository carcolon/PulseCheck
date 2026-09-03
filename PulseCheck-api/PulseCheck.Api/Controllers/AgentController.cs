using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PulseCheck.Api.Auth;
using PulseCheck.Api.Models;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Security;
using PulseCheck.Application.Services;
using PulseCheck.Domain.Entities;

namespace PulseCheck.Api.Controllers;

[ApiController]
[Route("api/agent")]
public sealed class AgentController(
    PulseCheckService pulseCheckService,
    IPulseCheckUnitOfWork unitOfWork,
    IHostEnvironment environment,
    IOptions<PulseCheckOptions> options) : ControllerBase
{
    private const int MinimumCredentialLifetimeDays = 30;

    [HttpPost("register")]
    public async Task<ActionResult<AgentRegistrationResponse>> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateProvisioningToken())
        {
            return Unauthorized(new { message = "Agent provisioning token is invalid." });
        }

        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return ValidationProblem("userId and deviceId are required.");
        }

        var device = await pulseCheckService.RegisterDeviceAsync(request, cancellationToken);
        if (device is null)
        {
            return ValidationProblem();
        }

        var agentToken = AgentSecurity.GenerateToken();
        var now = DateTimeOffset.UtcNow;
        var expiresAtUtc = now.AddDays(Math.Max(options.Value.AgentSecurity.CredentialLifetimeDays, MinimumCredentialLifetimeDays));
        var existingCredential = await unitOfWork.GetAgentCredentialByDeviceIdAsync(device.DeviceId, cancellationToken);
        if (existingCredential is null)
        {
            await unitOfWork.AddAgentCredentialAsync(
                new AgentCredential
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.DeviceId,
                    TokenHash = AgentSecurity.ComputeTokenHash(agentToken),
                    CreatedAtUtc = now,
                    ExpiresAtUtc = expiresAtUtc,
                    LastUsedAtUtc = now
                },
                cancellationToken);
        }
        else
        {
            if (!ValidateExistingAgentCredential(existingCredential, request.CurrentAgentToken))
            {
                return StatusCode(
                    StatusCodes.Status409Conflict,
                    new { message = "Device is already registered. Existing agent credential is required to rotate it." });
            }

            existingCredential.TokenHash = AgentSecurity.ComputeTokenHash(agentToken);
            existingCredential.RevokedAtUtc = null;
            existingCredential.ExpiresAtUtc = expiresAtUtc;
            existingCredential.LastUsedAtUtc = now;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(new AgentRegistrationResponse(device, agentToken));
    }

    [Authorize(AuthenticationSchemes = AgentTokenAuthenticationDefaults.Scheme)]
    [HttpPost("sync")]
    public async Task<ActionResult<AgentSyncResponse>> Sync(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return ValidationProblem("userId and deviceId are required.");
        }

        if (!RequestMatchesAuthenticatedDevice(request.DeviceId))
        {
            return Forbid();
        }

        var result = await pulseCheckService.SyncAgentAsync(request, cancellationToken);
        return result is null ? ValidationProblem() : Ok(result);
    }

    private bool ValidateProvisioningToken()
    {
        var expectedToken = options.Value.AgentSecurity.ProvisioningToken;
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return false;
        }

        if (!environment.IsDevelopment() && expectedToken.Trim().Length < 32)
        {
            return false;
        }

        var providedToken = ExtractBearerToken();
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var providedBytes = Encoding.UTF8.GetBytes(providedToken);
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static bool ValidateExistingAgentCredential(AgentCredential credential, string? providedToken)
    {
        if (string.IsNullOrWhiteSpace(providedToken) ||
            credential.RevokedAtUtc is not null ||
            credential.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var expectedHash = credential.TokenHash;
        var providedHash = AgentSecurity.ComputeTokenHash(providedToken);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        var providedBytes = Encoding.UTF8.GetBytes(providedHash);
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private string? ExtractBearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }

    private bool RequestMatchesAuthenticatedDevice(string deviceId)
    {
        var authenticatedDeviceId = User.FindFirstValue("device_id");
        return !string.IsNullOrWhiteSpace(authenticatedDeviceId) &&
               string.Equals(authenticatedDeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
