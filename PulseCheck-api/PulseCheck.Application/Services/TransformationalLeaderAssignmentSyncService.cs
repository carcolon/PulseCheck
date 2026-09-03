using PulseCheck.Application.Ports;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Domain.Entities;

namespace PulseCheck.Application.Services;

public sealed class TransformationalLeaderAssignmentSyncService(
    IPulseCheckUnitOfWork unitOfWork,
    IEmployeeOperationsProfileResolver employeeOperationsProfileResolver,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<TransformationalLeaderAssignmentSyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        var assignments = await unitOfWork.GetTransformationalLeaderAssignmentsAsync(cancellationToken);
        var activeLeaders = await employeeOperationsProfileResolver.GetTransformationalLeaderCandidatesAsync(cancellationToken);
        if (activeLeaders.Count == 0)
        {
            return new TransformationalLeaderAssignmentSyncResult(assignments.Count, 0, 0, 0, 0, 0, 0, true);
        }

        var now = dateTimeProvider.UtcNow;
        var activeLeaderSolvoIds = activeLeaders
            .Select(item => item.SolvoId.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cachedLeaders = await unitOfWork.GetTransformationalLeaderCandidatesAsync(activeOnly: false, cancellationToken);
        var cachedBySolvoId = cachedLeaders.ToDictionary(item => item.SolvoId, StringComparer.OrdinalIgnoreCase);
        var addedLeaders = 0;
        var updatedLeaders = 0;

        foreach (var leader in activeLeaders
                     .Where(item => !string.IsNullOrWhiteSpace(item.SolvoId))
                     .GroupBy(item => item.SolvoId.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            if (!cachedBySolvoId.TryGetValue(leader.SolvoId.Trim(), out var cached))
            {
                cached = new TransformationalLeaderCandidateCache
                {
                    Id = Guid.NewGuid(),
                    SolvoId = leader.SolvoId.Trim(),
                    CreatedAtUtc = now
                };
                await unitOfWork.AddTransformationalLeaderCandidateAsync(cached, cancellationToken);
                addedLeaders++;
            }
            else
            {
                updatedLeaders++;
            }

            cached.FullName = Normalize(leader.FullName, 180);
            cached.CorporateEmail = Normalize(leader.CorporateEmail, 180).ToLowerInvariant();
            cached.JobTitleCode = Normalize(leader.JobTitleCode, 80);
            cached.Status = Normalize(leader.Status, 80);
            cached.Operation = Normalize(leader.Operation, 180);
            cached.Client = Normalize(leader.Client, 180);
            cached.Department = Normalize(leader.Department, 180);
            cached.IsActive = true;
            cached.UpdatedAtUtc = now;
            cached.LastSyncedAtUtc = now;
        }

        var deactivatedLeaders = 0;
        foreach (var cached in cachedLeaders.Where(item => item.IsActive && !activeLeaderSolvoIds.Contains(item.SolvoId.Trim())))
        {
            cached.IsActive = false;
            cached.UpdatedAtUtc = now;
            cached.LastSyncedAtUtc = now;
            deactivatedLeaders++;
        }

        var invalidAssignments = assignments
            .Where(item => !activeLeaderSolvoIds.Contains(item.SolvoId.Trim()))
            .ToArray();

        if (invalidAssignments.Length == 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new TransformationalLeaderAssignmentSyncResult(
                assignments.Count,
                activeLeaderSolvoIds.Count,
                addedLeaders,
                updatedLeaders,
                deactivatedLeaders,
                0,
                0,
                false);
        }

        var invalidSolvoIds = invalidAssignments.Select(item => item.SolvoId).ToArray();
        var activeSessions = await unitOfWork.GetActiveTransformationalLeaderSessionsBySolvoIdsAsync(invalidSolvoIds, now, cancellationToken);

        foreach (var assignment in invalidAssignments)
        {
            await unitOfWork.RemoveTransformationalLeaderAssignmentAsync(assignment, cancellationToken);
        }

        foreach (var session in activeSessions)
        {
            session.RevokedAtUtc = now;
            session.ExpiresAtUtc = now;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TransformationalLeaderAssignmentSyncResult(
            assignments.Count,
            activeLeaderSolvoIds.Count,
            addedLeaders,
            updatedLeaders,
            deactivatedLeaders,
            invalidAssignments.Length,
            activeSessions.Count,
            false);
    }

    private static string Normalize(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

public sealed record TransformationalLeaderAssignmentSyncResult(
    int AssignmentCount,
    int ActiveLeaderCount,
    int AddedLeaderCount,
    int UpdatedLeaderCount,
    int DeactivatedLeaderCount,
    int RemovedAssignmentCount,
    int RevokedSessionCount,
    bool SkippedBecauseActiveLeadersUnavailable);
