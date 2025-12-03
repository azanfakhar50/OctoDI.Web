using System.Threading.Tasks;

namespace OctoDI.Web.Services
{
    // Interface defines WHAT the service does
    public interface IOtpService
    {
        // Generate a 6-digit OTP code
        string GenerateOtp();

        // Send OTP code to user's email
        Task SendOtpEmailAsync(string email, string code);
    }
}
