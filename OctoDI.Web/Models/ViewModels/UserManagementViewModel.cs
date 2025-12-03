namespace OctoDI.Web.Models.ViewModels
{
    public class UserManagementViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string UserRole { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }

        public int SubscriptionId { get; set; }
        public string CompanyName { get; set; } = "";
        public string SubscriptionType { get; set; } = "";
    }
}
