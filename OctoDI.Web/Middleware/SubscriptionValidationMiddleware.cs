using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OctoDI.Web.Hubs;
using OctoDI.Web.Models.DatabaseModels;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OctoDI.Web.Middleware
{
    public class SubscriptionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public SubscriptionValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IHubContext<SubscriptionHub> hub)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // ============================================================
            // 1️⃣ ALWAYS ALLOW: Public paths (login, logout, static files)
            // ============================================================
            if (path.StartsWith("/account/login") ||
                path.StartsWith("/account/logout") ||
                path.StartsWith("/account/accessdenied") ||
                path.StartsWith("/css") ||
                path.StartsWith("/js") ||
                path.StartsWith("/lib") ||
                path.StartsWith("/images") ||
                path.StartsWith("/favicon") ||
                path.StartsWith("/_framework") ||
                path.StartsWith("/signalr"))
            {
                await _next(context);
                return;
            }

            // ============================================================
            // 2️⃣ IF NOT AUTHENTICATED: Allow request (Authorization will handle)
            // ============================================================
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            // ============================================================
            // 3️⃣ GET USER ROLE
            // ============================================================
            var userRole = context.User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userRole))
            {
                Console.WriteLine("❌ No role found in claims - Logging out");
                await LogoutUser(context, "No role found");
                return;
            }

            // ============================================================
            // 4️⃣ SUPERADMIN: Skip all subscription checks
            // ============================================================
            if (userRole == "SuperAdmin")
            {
                // ✅ Allow ALL paths for SuperAdmin
                await _next(context);
                return;
            }

            // ============================================================
            // 5️⃣ SUBSCRIPTION ADMIN/USER: Validate subscription
            // ============================================================
            var subscriptionIdClaim = context.User.FindFirst("SubscriptionId")?.Value;
            var userIdClaim = context.User.FindFirst("UserId")?.Value;

            // Check if required claims exist
            if (string.IsNullOrEmpty(subscriptionIdClaim))
            {
                Console.WriteLine($"❌ SubscriptionId claim missing for user role: {userRole}");
                await LogoutUser(context, "Missing subscription information");
                return;
            }

            if (string.IsNullOrEmpty(userIdClaim))
            {
                Console.WriteLine($"❌ UserId claim missing for user role: {userRole}");
                await LogoutUser(context, "Missing user information");
                return;
            }

            // Parse claims
            if (!int.TryParse(subscriptionIdClaim, out int subscriptionId))
            {
                Console.WriteLine($"❌ Invalid SubscriptionId format: {subscriptionIdClaim}");
                await LogoutUser(context, "Invalid subscription format");
                return;
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                Console.WriteLine($"❌ Invalid UserId format: {userIdClaim}");
                await LogoutUser(context, "Invalid user format");
                return;
            }

            // ============================================================
            // 6️⃣ FETCH SUBSCRIPTION FROM DATABASE
            // ============================================================
            var subscription = await db.Subscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (subscription == null)
            {
                Console.WriteLine($"❌ Subscription {subscriptionId} not found in database");
                await NotifyAndLogout(context, hub, userId, "Subscription not found! Contact admin.");
                return;
            }

            // ============================================================
            // 7️⃣ VALIDATE SUBSCRIPTION STATUS
            // ============================================================

            // Check: Blocked
            if (subscription.IsBlocked)
            {
                Console.WriteLine($"🚫 Subscription {subscriptionId} is BLOCKED");
                await NotifyAndLogout(context, hub, userId, "Your subscription has been blocked by admin!");
                return;
            }

            // Check: Inactive
            if (!subscription.IsActive)
            {
                Console.WriteLine($"⚠️ Subscription {subscriptionId} is INACTIVE");
                await NotifyAndLogout(context, hub, userId, "Your subscription is inactive! Contact admin.");
                return;
            }

            // Check: Subscription expired
            if (subscription.SubscriptionEndDate.HasValue &&
                subscription.SubscriptionEndDate.Value < DateTime.Now)
            {
                Console.WriteLine($"⏰ Subscription {subscriptionId} EXPIRED on {subscription.SubscriptionEndDate.Value}");

                // Mark as inactive in DB
                subscription.IsActive = false;
                db.Subscriptions.Attach(subscription);
                db.Entry(subscription).Property(s => s.IsActive).IsModified = true;
                await db.SaveChangesAsync();

                await NotifyAndLogout(context, hub, userId, "Your subscription has expired! Please renew.");
                return;
            }

            // Check: Token expired
            if (subscription.TokenExpiryDate.HasValue &&
                subscription.TokenExpiryDate.Value < DateTime.Now)
            {
                Console.WriteLine($"🔑 Token for subscription {subscriptionId} EXPIRED on {subscription.TokenExpiryDate.Value}");
                await NotifyAndLogout(context, hub, userId, "Your subscription token has expired! Contact admin.");
                return;
            }

            // ============================================================
            // 8️⃣ ALL CHECKS PASSED - Allow request
            // ============================================================
            Console.WriteLine($"✅ Subscription {subscriptionId} validation PASSED for user {userId}");
            await _next(context);
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private async Task LogoutUser(HttpContext context, string reason)
        {
            Console.WriteLine($"🚪 Logging out user: {reason}");
            await context.SignOutAsync();
            context.Session.Clear();
            context.Response.Redirect("/Account/Login");
        }

        private async Task NotifyAndLogout(HttpContext context, IHubContext<SubscriptionHub> hub, int userId, string message)
        {
            Console.WriteLine($"🚪 Notifying and logging out user {userId}: {message}");

            // Send notification via SignalR
            try
            {
                await hub.Clients.User(userId.ToString())
                    .SendAsync("ReceiveUpdate", message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to send SignalR notification: {ex.Message}");
            }

            // Logout user
            await context.SignOutAsync();
            context.Session.Clear();
            context.Response.Redirect("/Account/Login");
        }
    }
}