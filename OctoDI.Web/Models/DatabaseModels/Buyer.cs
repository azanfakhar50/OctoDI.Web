using OctoDI.Web.Models.DatabaseModels;
using System.ComponentModel.DataAnnotations;

namespace OctoDI.Web.Models.DatabaseModels
{
    public partial class Buyer
    {
        public int BuyerId { get; set; }
        public string BuyerNTN { get; set; } = null!;
        public string BuyerBusinessName { get; set; } = null!;
        public string? BuyerAddress { get; set; }
        public string? BuyerProvince { get; set; }
        public string? BuyerRegistrationType { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }

        // Foreign key
        public int SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }
    }
}

