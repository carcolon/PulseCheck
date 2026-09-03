using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;

namespace PulseCheck.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/diagnostics")]
public sealed class DiagnosticsController(
    IHostEnvironment environment,
    IEmployeeOperationsProfileResolver employeeOperationsProfileResolver) : ControllerBase
{
    [HttpGet("fabric/people-columns")]
    public async Task<ActionResult<FabricPeopleColumnsDiagnosticsDto>> GetFabricPeopleColumns(
        [FromQuery] int sampleSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var diagnostics = await employeeOperationsProfileResolver.GetPeopleColumnsDiagnosticsAsync(sampleSize, cancellationToken);
        return diagnostics is null
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Fabric diagnostics are not available." })
            : Ok(diagnostics);
    }

    [HttpGet("fabric/employee-profile")]
    public async Task<ActionResult<FabricEmployeeProfileDiagnosticsDto>> GetFabricEmployeeProfile(
        [FromQuery] string email,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return ValidationProblem("Query parameter email is required.");
        }

        try
        {
            var diagnostics = await employeeOperationsProfileResolver.GetEmployeeProfileDiagnosticsByEmailAsync(email, cancellationToken);
            return diagnostics is null
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Fabric employee profile diagnostics are not available." })
                : Ok(diagnostics);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }
}
