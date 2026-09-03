using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Security;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Domain.Entities;

namespace PulseCheck.Application.Services;

public sealed class TransformationalLeaderAuthService(
    IPulseCheckUnitOfWork unitOfWork,
    IEmployeeOperationsProfileResolver employeeOperationsProfileResolver,
    IDateTimeProvider dateTimeProvider)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(1);
    public const string OwnerOperationScope = "All operations";

    public async Task<TransformationalLeaderSessionDto?> LoginWithEntraAsync(
        string email,
        string displayName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        var adminUser = await unitOfWork.GetAdminUserByEmailAsync(normalizedEmail, cancellationToken);
        if (adminUser is not null
            && adminUser.IsActive
            && string.Equals(adminUser.AuthenticationMode, "Entra", StringComparison.OrdinalIgnoreCase)
            && AdminRoles.IsOwner(adminUser.Role))
        {
            return await CreateSessionAsync(
                adminUser.Id.ToString("N"),
                normalizedEmail,
                string.IsNullOrWhiteSpace(displayName) ? adminUser.DisplayName : displayName,
                [OwnerOperationScope],
                ipAddress,
                userAgent,
                cancellationToken);
        }

        var leaders = await GetActiveLeadersAsync(cancellationToken);
        var leader = leaders.FirstOrDefault(item => string.Equals(item.CorporateEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase));
        if (leader is null)
        {
            return null;
        }

        var assignment = await unitOfWork.GetTransformationalLeaderAssignmentBySolvoIdAsync(leader.SolvoId, cancellationToken);
        if (assignment is null || string.IsNullOrWhiteSpace(assignment.Operation))
        {
            return null;
        }

        var assignedOperations = TransformationalLeaderOperationScope.Parse(assignment.OperationsJson, assignment.Operation);
        if (assignedOperations.Count == 0)
        {
            return null;
        }

        return await CreateSessionAsync(
            leader.SolvoId,
            normalizedEmail,
            string.IsNullOrWhiteSpace(leader.FullName) ? displayName : leader.FullName,
            assignedOperations,
            ipAddress,
            userAgent,
            cancellationToken);
    }

    public async Task<TransformationalLeaderSessionDto?> GetSessionAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var session = await unitOfWork.GetTransformationalLeaderSessionByTokenHashAsync(AdminSecurity.ComputeTokenHash(token), cancellationToken);
        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= dateTimeProvider.UtcNow)
        {
            return null;
        }

        session.ExpiresAtUtc = dateTimeProvider.UtcNow.Add(SessionLifetime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(token, session);
    }

    public async Task<bool> LogoutAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var session = await unitOfWork.GetTransformationalLeaderSessionByTokenHashAsync(AdminSecurity.ComputeTokenHash(token), cancellationToken);
        if (session is null || session.RevokedAtUtc is not null)
        {
            return false;
        }

        session.RevokedAtUtc = dateTimeProvider.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<TransformationalLeaderSessionDto> CreateSessionAsync(
        string solvoId,
        string email,
        string displayName,
        IReadOnlyList<string> operations,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var token = AdminSecurity.GenerateSessionToken();
        var normalizedOperations = TransformationalLeaderOperationScope.Normalize(operations);
        var session = new TransformationalLeaderSession
        {
            Id = Guid.NewGuid(),
            SolvoId = solvoId,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName.Trim(),
            Operation = TransformationalLeaderOperationScope.Format(normalizedOperations),
            OperationsJson = TransformationalLeaderOperationScope.Serialize(normalizedOperations),
            TokenHash = AdminSecurity.ComputeTokenHash(token),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(SessionLifetime)
        };

        await unitOfWork.AddTransformationalLeaderSessionAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(token, session);
    }

    private static TransformationalLeaderSessionDto ToDto(string token, TransformationalLeaderSession session)
        => new(
            token,
            string.Empty,
            session.ExpiresAtUtc,
            new AdminUserDto(Guid.Empty, session.Email, session.DisplayName, "TransformationalLeader"),
            session.SolvoId,
            session.Operation,
            TransformationalLeaderOperationScope.Parse(session.OperationsJson, session.Operation));

    private async Task<IReadOnlyList<TransformationalLeaderCandidate>> GetActiveLeadersAsync(CancellationToken cancellationToken)
    {
        var cachedLeaders = await unitOfWork.GetTransformationalLeaderCandidatesAsync(activeOnly: true, cancellationToken);
        if (cachedLeaders.Count > 0)
        {
            return cachedLeaders
                .Select(item => new TransformationalLeaderCandidate(
                    item.SolvoId,
                    item.FullName,
                    item.CorporateEmail,
                    item.JobTitleCode,
                    item.Status,
                    item.Operation,
                    item.Client,
                    item.Department))
                .ToArray();
        }

        return await employeeOperationsProfileResolver.GetTransformationalLeaderCandidatesAsync(cancellationToken);
    }
}
