using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace PulseCheck.Api.Hubs;

public sealed class NotificationHub : Hub
{
    public async Task SubscribeToDevice(string deviceId)
    {
        if (!RequestMatchesAuthenticatedDevice(deviceId))
        {
            throw new HubException("The authenticated agent cannot subscribe to this device.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceId}");
    }

    public async Task SubscribeToUser(string userId)
    {
        if (!RequestMatchesAuthenticatedUser(userId))
        {
            throw new HubException("The authenticated agent cannot subscribe to this user.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
    }

    private bool RequestMatchesAuthenticatedDevice(string deviceId)
    {
        var authenticatedDeviceId = Context.User?.FindFirst("device_id")?.Value;
        return !string.IsNullOrWhiteSpace(authenticatedDeviceId) &&
               string.Equals(authenticatedDeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private bool RequestMatchesAuthenticatedUser(string userId)
    {
        var authenticatedUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(authenticatedUserId) &&
               string.Equals(authenticatedUserId, userId.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
