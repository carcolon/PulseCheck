using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Security;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Domain.Entities;

namespace PulseCheck.Application.Services;

public sealed class AdminAuthService(
    IPulseCheckUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(1);

    public async Task<AdminSessionDto?> LoginAsync(
        AdminLoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var user = await unitOfWork.GetAdminUserByEmailAsync(email, cancellationToken);
        if (user is null
            || !user.IsActive
            || !string.Equals(user.AuthenticationMode, "Local", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(user.PasswordHash)
            || !AdminSecurity.VerifyPassword(request.Password, user.PasswordHash))
        {
            return null;
        }

        return await CreateSessionAsync(user, ipAddress, userAgent, cancellationToken);
    }

    public async Task<AdminSessionDto?> LoginWithEntraAsync(
        string email,
        string displayName,
        string? entraObjectId,
        string? tenantId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        var user = await unitOfWork.GetAdminUserByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null && await unitOfWork.CountAdminUsersAsync(cancellationToken) == 0)
        {
            user = new AdminUser
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim(),
                EntraObjectId = string.IsNullOrWhiteSpace(entraObjectId) ? null : entraObjectId.Trim(),
                TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim(),
                AuthenticationMode = "Entra",
                Role = "Owner",
                PasswordHash = string.Empty,
                IsActive = true,
                CreatedAtUtc = dateTimeProvider.UtcNow
            };

            await unitOfWork.AddAdminUserAsync(user, cancellationToken);
        }

        if (user is null
            || !user.IsActive
            || !string.Equals(user.AuthenticationMode, "Entra", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        user.Email = normalizedEmail;

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName.Trim();
        }

        user.EntraObjectId = string.IsNullOrWhiteSpace(entraObjectId) ? null : entraObjectId.Trim();
        user.TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();

        return await CreateSessionAsync(user, ipAddress, userAgent, cancellationToken);
    }

    public async Task<AdminSessionDto?> GetSessionAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var session = await unitOfWork.GetAdminSessionByTokenHashAsync(AdminSecurity.ComputeTokenHash(token), cancellationToken);
        if (session?.AdminUser is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= dateTimeProvider.UtcNow || !session.AdminUser.IsActive)
        {
            return null;
        }

        session.ExpiresAtUtc = dateTimeProvider.UtcNow.Add(SessionLifetime);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AdminSessionDto(
            token,
            string.Empty,
            session.ExpiresAtUtc,
            new AdminUserDto(
                session.AdminUser.Id,
                session.AdminUser.Email,
                session.AdminUser.DisplayName,
                AdminRoles.NormalizeForStorage(session.AdminUser.Role)));
    }

    public async Task<bool> LogoutAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var session = await unitOfWork.GetAdminSessionByTokenHashAsync(AdminSecurity.ComputeTokenHash(token), cancellationToken);
        if (session is null || session.RevokedAtUtc is not null)
        {
            return false;
        }

        session.RevokedAtUtc = dateTimeProvider.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> IsAuthorizedCorporateEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var user = await unitOfWork.GetAdminUserByEmailAsync(email, cancellationToken);
        return user is { IsActive: true }
            && string.Equals(user.AuthenticationMode, "Entra", StringComparison.OrdinalIgnoreCase);
    }

    public async Task EnsureBootstrapAdminAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken)
    {
        if (await unitOfWork.CountAdminUsersAsync(cancellationToken) > 0)
        {
            return;
        }

        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            AuthenticationMode = "Local",
            Role = "Owner",
            PasswordHash = AdminSecurity.HashPassword(password),
            IsActive = true,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        await unitOfWork.AddAdminUserAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<AdminSessionDto> CreateSessionAsync(
        AdminUser user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var token = AdminSecurity.GenerateSessionToken();
        var session = new AdminSession
        {
            Id = Guid.NewGuid(),
            AdminUserId = user.Id,
            TokenHash = AdminSecurity.ComputeTokenHash(token),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(SessionLifetime)
        };

        user.LastLoginAtUtc = now;
        await unitOfWork.AddAdminSessionAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AdminSessionDto(
            token,
            string.Empty,
            session.ExpiresAtUtc,
            new AdminUserDto(user.Id, user.Email, user.DisplayName, AdminRoles.NormalizeForStorage(user.Role)));
    }
}
