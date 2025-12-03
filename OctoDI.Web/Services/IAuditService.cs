using System.Threading.Tasks;

namespace YourProject.Services
{
    public interface IAuditService
    {
        Task LogAsync(
            string action,
            string role = null,
            int? subscriptionId = null,
            string companyName = null,
            string userId = null,
            string userName = null
        );
    }
}