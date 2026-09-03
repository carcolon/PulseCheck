using System.Net.Mail;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Security;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Domain.Entities;

namespace PulseCheck.Application.Services;

public sealed class AdminUserService(
    IPulseCheckUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<IReadOnlyList<AdminAccountDto>> GetAdminsAsync(CancellationToken cancellationToken)
    {
        var users = await unitOfWork.GetAdminUsersAsync(cancellationToken);
        return users
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Email)
            .Select(ToDto)
            .ToArray();
    }

    public async Task<AdminAccountDto?> CreateEntraAdminAsync(CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (email is null)
        {
            return null;
        }

        var existing = await unitOfWork.GetAdminUserByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            existing.Email = email;
            existing.AuthenticationMode = "Entra";
            existing.PasswordHash = string.Empty;
            existing.Role = AdminRoles.NormalizeForStorage(request.Roles);
            existing.IsActive = true;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ToDto(existing);
        }

        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = email,
            AuthenticationMode = "Entra",
            Role = AdminRoles.NormalizeForStorage(request.Roles),
            PasswordHash = string.Empty,
            IsActive = true,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        await unitOfWork.AddAdminUserAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task<DeleteAdminUserResult> DeleteAdminAsync(
        Guid id,
        Guid currentAdminUserId,
        CancellationToken cancellationToken)
    {
        if (id == currentAdminUserId)
        {
            return DeleteAdminUserResult.CannotDeleteSelf;
        }

        var user = await unitOfWork.GetAdminUserByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return DeleteAdminUserResult.NotFound;
        }

        var admins = await unitOfWork.GetAdminUsersAsync(cancellationToken);
        var activeAdminCount = admins.Count(item => item.IsActive);
        if (user.IsActive && activeAdminCount <= 1)
        {
            return DeleteAdminUserResult.LastActiveAdmin;
        }

        var activeOwnerCount = admins.Count(IsActiveOwner);
        if (IsActiveOwner(user) && activeOwnerCount <= 1)
        {
            return DeleteAdminUserResult.LastActiveOwner;
        }

        await unitOfWork.RemoveAdminUserAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DeleteAdminUserResult.Deleted;
    }

    public async Task<UpdateAdminUserStatusResult> UpdateAdminStatusAsync(
        Guid id,
        Guid currentAdminUserId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var user = await unitOfWork.GetAdminUserByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return new UpdateAdminUserStatusResult(UpdateAdminUserStatusCode.NotFound, null);
        }

        if (!isActive && id == currentAdminUserId)
        {
            return new UpdateAdminUserStatusResult(UpdateAdminUserStatusCode.CannotDeactivateSelf, null);
        }

        if (!isActive && user.IsActive)
        {
            var admins = await unitOfWork.GetAdminUsersAsync(cancellationToken);
            var activeAdminCount = admins.Count(item => item.IsActive);
            if (activeAdminCount <= 1)
            {
                return new UpdateAdminUserStatusResult(UpdateAdminUserStatusCode.LastActiveAdmin, null);
            }

            var activeOwnerCount = admins.Count(IsActiveOwner);
            if (IsActiveOwner(user) && activeOwnerCount <= 1)
            {
                return new UpdateAdminUserStatusResult(UpdateAdminUserStatusCode.LastActiveOwner, null);
            }
        }

        user.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new UpdateAdminUserStatusResult(UpdateAdminUserStatusCode.Updated, ToDto(user));
    }

    private static AdminAccountDto ToDto(AdminUser user)
        => new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.AuthenticationMode,
            AdminRoles.NormalizeForStorage(user.Role),
            user.IsActive,
            user.CreatedAtUtc,
            user.LastLoginAtUtc);

    private static bool IsActiveOwner(AdminUser user)
        => user.IsActive && AdminRoles.IsOwner(user.Role);

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var email = value.Trim().ToLowerInvariant();
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase) ? email : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

public enum DeleteAdminUserResult
{
    Deleted,
    NotFound,
    CannotDeleteSelf,
    LastActiveAdmin,
    LastActiveOwner
}

public sealed record UpdateAdminUserStatusResult(UpdateAdminUserStatusCode Code, AdminAccountDto? Admin);

public enum UpdateAdminUserStatusCode
{
    Updated,
    NotFound,
    CannotDeactivateSelf,
    LastActiveAdmin,
    LastActiveOwner
}
