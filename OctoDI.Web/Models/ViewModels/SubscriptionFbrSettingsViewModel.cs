namespace OctoDI.Web.Models.ViewModels
{
    public class SubscriptionFbrSettingsViewModel
    {
        public int SubscriptionId { get; set; }
        public string? SubscriptionName { get; set; }
        public string? FbrBaseUrl { get; set; }
        public string? FbrToken { get; set; }
        public string? SellerNTN { get; set; }
        public string? SellerBusinessName { get; set; }
        public string? SellerAddress { get; set; }
        public string? SellerProvince { get; set; }

    }

}
