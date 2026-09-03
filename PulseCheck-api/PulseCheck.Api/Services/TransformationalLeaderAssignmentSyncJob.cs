using Hangfire;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Services;

public sealed class TransformationalLeaderAssignmentSyncJob(
    TransformationalLeaderAssignmentSyncService syncService,
    ILogger<TransformationalLeaderAssignmentSyncJob> logger)
{
    public const string JobId = "sync-transformational-leader-assignments";

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync()
    {
        var result = await syncService.SyncAsync(CancellationToken.None);

        if (result.SkippedBecauseActiveLeadersUnavailable)
        {
            logger.LogWarning(
                "Transformational Leader assignment sync skipped cleanup because no active leaders were returned from Wolfpack. AssignmentCount={AssignmentCount}.",
                result.AssignmentCount);
            return;
        }

        logger.LogInformation(
            "Transformational Leader assignment sync finished. AssignmentCount={AssignmentCount}, ActiveLeaderCount={ActiveLeaderCount}, AddedLeaders={AddedLeaders}, UpdatedLeaders={UpdatedLeaders}, DeactivatedLeaders={DeactivatedLeaders}, RemovedAssignments={RemovedAssignments}, RevokedSessions={RevokedSessions}.",
            result.AssignmentCount,
            result.ActiveLeaderCount,
            result.AddedLeaderCount,
            result.UpdatedLeaderCount,
            result.DeactivatedLeaderCount,
            result.RemovedAssignmentCount,
            result.RevokedSessionCount);
    }
}
