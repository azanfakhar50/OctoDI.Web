using Microsoft.AspNetCore.SignalR;

namespace OctoDI.Web.Hubs
{
    public class SubscriptionHub : Hub
    {
        // Send update to a specific user
        public async Task SendUpdateToUser(string userId, string message)
        {
            await Clients.User(userId).SendAsync("ReceiveUpdate", message);
        }

        // Send update to all connected users
        public async Task SendUpdateToAll(string message)
        {
            await Clients.All.SendAsync("ReceiveUpdate", message);
        }
    }
}
