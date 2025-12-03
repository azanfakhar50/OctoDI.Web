using Microsoft.AspNetCore.Http;
using OctoDI.Web.Models.DatabaseModels;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using OctoDI.Web.Models;

namespace YourProject.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(
            string action,
            string role = null,
            int? subscriptionId = null,
            string companyName = null,
            string userId = null,
            string userName = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ip = httpContext?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();

            // Use passed values first, fallback to claims if not provided
            var finalUserId = userId ?? httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var finalUserName = userName ?? httpContext?.User?.Identity?.Name;

            if (string.IsNullOrEmpty(role))
            {
                role = httpContext?.User?.FindFirstValue(ClaimTypes.Role);
            }

            var audit = new AuditLog
            {
                UserId = finalUserId,
                UserName = finalUserName,
                Role = role,
                SubscriptionId = subscriptionId,
                CompanyName = companyName,
                Action = action,
                IPAddress = ip,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
    }
}