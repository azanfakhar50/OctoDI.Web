using OctoDI.Web.Models; // Add this if ApplicationUser is defined in OctoDI.Web.Models

namespace OctoDI.Web.Models.DatabaseModels
{
    public class TwoFactorOtp
    {
        public int Id { get; set; }

        public int UserId { get; set; }         // FK to AspNetUsers (or your user table)
        public User User { get; set; }

        public string Code { get; set; }
        public DateTime ExpiryTime { get; set; }

        public bool IsUsed { get; set; }
    }
}
