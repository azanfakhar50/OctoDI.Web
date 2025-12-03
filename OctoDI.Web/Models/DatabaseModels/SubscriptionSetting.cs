using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OctoDI.Web.Models.DatabaseModels
{
    [Table("SubscriptionSettings")]
    public class SubscriptionSetting
    {
        [Key]
        public int SettingId { get; set; }

        [Required]
        public int SubscriptionId { get; set; }

        // ✅ Changed: Made nullable to handle NULL values from database
        [StringLength(250)]
        public string? FbrBaseUrl { get; set; }

        // ✅ Changed: Made nullable to handle NULL values from database
        public string? FbrToken { get; set; }

        // ✅ Changed: Made nullable to handle NULL values from database
        [StringLength(15)]
        public string? SellerNTN { get; set; }

        // ✅ Changed: Made nullable to handle NULL values from database
        [StringLength(150)]
        public string? SellerBusinessName { get; set; }

        [StringLength(250)]
        public string? SellerAddress { get; set; }

        [StringLength(5)]
        public string? SellerProvince { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string? CreatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public string? UpdatedBy { get; set; }

        public string? Remarks { get; set; }

        [ForeignKey("SubscriptionId")]
        public virtual Subscription? Subscription { get; set; }
    }
}