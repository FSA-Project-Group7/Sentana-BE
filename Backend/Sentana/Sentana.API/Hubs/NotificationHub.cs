using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Sentana.API.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
    }
}
