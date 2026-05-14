using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace Project_API.Hubs;
[Authorize]
public class NotificationHub : Hub {
    public override async Task OnConnectedAsync() {
        await base.OnConnectedAsync();
    }
}