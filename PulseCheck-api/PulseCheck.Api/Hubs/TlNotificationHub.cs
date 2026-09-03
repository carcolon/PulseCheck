using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PulseCheck.Api.Auth;

namespace PulseCheck.Api.Hubs;

[Authorize(AuthenticationSchemes = TransformationalLeaderAuthenticationDefaults.Scheme, Roles = "TransformationalLeader")]
public sealed class TlNotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var sessionId = Context.User?.FindFirst("tl_session_id")?.Value;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildSessionGroup(sessionId));
        }

        await base.OnConnectedAsync();
    }

    public static string BuildSessionGroup(Guid sessionId)
        => BuildSessionGroup(sessionId.ToString("D"));

    private static string BuildSessionGroup(string sessionId)
        => $"tl-session:{sessionId}";
}
