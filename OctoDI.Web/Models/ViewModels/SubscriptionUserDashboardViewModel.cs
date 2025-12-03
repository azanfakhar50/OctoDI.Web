namespace OctoDI.Web.Models.ViewModels
{
    public class SubscriptionUserDashboardViewModel
    {
        public int SubscriptionId { get; set; }
        public int UserId { get; set; }

        public int TotalInvoices { get; set; }
        public int TotalBuyers { get; set; }
        public int TotalProducts { get; set; }
    }

}
