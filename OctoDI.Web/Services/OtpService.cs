using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace OctoDI.Web.Services
{
    // Implements the interface
    public class OtpService : IOtpService
    {
        private readonly IConfiguration _config;

        // IConfiguration injected via DI to read SMTP settings
        public OtpService(IConfiguration config)
        {
            _config = config;
        }

        // Generate random 6-digit OTP
        public string GenerateOtp()
        {
            var rnd = new Random();
            return rnd.Next(100000, 999999).ToString();
        }

        // Send OTP to email
        public async Task SendOtpEmailAsync(string email, string code)
        {
            var smtpHost = _config["Smtp:Host"];
            var smtpPort = int.Parse(_config["Smtp:Port"]);
            var smtpUser = _config["Smtp:User"];
            var smtpPass = _config["Smtp:Pass"];

            var message = new MailMessage();
            message.To.Add(email);
            message.Subject = "OctoDI Two-Step Verification Code";
            message.Body = $"Your OTP code is: {code}";
            message.From = new MailAddress(smtpUser);
            message.IsBodyHtml = false;

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            await client.SendMailAsync(message);
        }
    }
}
