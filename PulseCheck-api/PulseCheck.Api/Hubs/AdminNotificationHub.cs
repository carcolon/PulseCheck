using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PulseCheck.Api.Auth;

namespace PulseCheck.Api.Hubs;

[Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme)]
public sealed class AdminNotificationHub : Hub
{
}
