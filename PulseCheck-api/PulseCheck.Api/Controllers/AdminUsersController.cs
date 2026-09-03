using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using PulseCheck.Api.Auth;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner")]
[EnableRateLimiting("admin-api")]
[Route("api/admin-users")]
public sealed class AdminUsersController(AdminUserService adminUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminAccountDto>>> GetAdmins(CancellationToken cancellationToken)
    {
        var admins = await adminUserService.GetAdminsAsync(cancellationToken);
        return Ok(admins);
    }

    [HttpPost]
    public async Task<ActionResult<AdminAccountDto>> CreateAdmin(
        [FromBody] CreateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await adminUserService.CreateEntraAdminAsync(request, cancellationToken);
        if (admin is null)
        {
            return ValidationProblem("Debes ingresar un correo electronico valido.");
        }

        return CreatedAtAction(nameof(GetAdmins), new { id = admin.Id }, admin);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAdmin(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentAdminUserId))
        {
            return Unauthorized();
        }

        var result = await adminUserService.DeleteAdminAsync(id, currentAdminUserId, cancellationToken);
        return result switch
        {
            DeleteAdminUserResult.Deleted => NoContent(),
            DeleteAdminUserResult.NotFound => NotFound(),
            DeleteAdminUserResult.CannotDeleteSelf => BadRequest(new { message = "No puedes eliminar tu propio usuario admin." }),
            DeleteAdminUserResult.LastActiveAdmin => BadRequest(new { message = "No puedes eliminar el ultimo admin activo." }),
            DeleteAdminUserResult.LastActiveOwner => BadRequest(new { message = "No puedes eliminar el ultimo owner activo." }),
            _ => BadRequest()
        };
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AdminAccountDto>> UpdateAdminStatus(
        Guid id,
        [FromBody] UpdateAdminUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentAdminUserId))
        {
            return Unauthorized();
        }

        var result = await adminUserService.UpdateAdminStatusAsync(id, currentAdminUserId, request.IsActive, cancellationToken);
        return result.Code switch
        {
            UpdateAdminUserStatusCode.Updated => Ok(result.Admin),
            UpdateAdminUserStatusCode.NotFound => NotFound(),
            UpdateAdminUserStatusCode.CannotDeactivateSelf => BadRequest(new { message = "No puedes desactivar tu propio usuario admin." }),
            UpdateAdminUserStatusCode.LastActiveAdmin => BadRequest(new { message = "No puedes desactivar el ultimo admin activo." }),
            UpdateAdminUserStatusCode.LastActiveOwner => BadRequest(new { message = "No puedes desactivar el ultimo owner activo." }),
            _ => BadRequest()
        };
    }
}
