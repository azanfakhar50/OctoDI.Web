using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OctoDI.Web.Models.DatabaseModels;
using OctoDI.Web.Hubs;

namespace OctoDI.Web.Services
{
    public class TokenExpiryService : IHostedService, IDisposable
    {
        private Timer _timer;
        private readonly IServiceProvider _serviceProvider;

        public TokenExpiryService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Run immediately and then every 5 minutes
            _timer = new Timer(async _ => await UpdateExpiredTokensAsync(null), null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

            return Task.CompletedTask;
        }

        private async Task UpdateExpiredTokensAsync(object state)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<SubscriptionHub>>();

            var expiredTokens = db.Subscriptions
                .Where(s => s.TokenExpiryDate != null
                            && s.TokenExpiryDate <= DateTime.UtcNow
                            && s.IsActive)
                .Include(s => s.Users);
           
            foreach (var sub in expiredTokens)
            {
                sub.IsActive = false;
                sub.UpdatedDate = DateTime.UtcNow;
                sub.Remarks = "Token automatically expired";

                if (sub.Users != null)
                {
                    foreach (var user in sub.Users)
                    {
                        try
                        {
                            await hub.Clients.User(user.UserId.ToString())
                                     .SendAsync("ReceiveUpdate", "Your subscription token has expired!");
                        }
                        catch { }
                    }
                }
            }

            await db.SaveChangesAsync();
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose() => _timer?.Dispose();
    }
}
