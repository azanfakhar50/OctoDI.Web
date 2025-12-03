using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OctoDI.Web.Models.DatabaseModels
{
    public partial class Subscription
    {
        [Key]
        public int SubscriptionId { get; set; }

        [MaxLength(50)]
        [Display(Name = "FBR Subscription ID")]
        public string? FbrSubscriptionId { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        [Display(Name = "NTN / CNIC")]
        public string NtnCnic { get; set; } = null!;
        [Required]
        public string SubscriptionType { get; set; } 


        [MaxLength(200)]
        [Display(Name = "Business Name")]
        public string? BusinessName { get; set; }

        [MaxLength(100)]
        [Display(Name = "Province")]
        public string? Province { get; set; }

        [MaxLength(500)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [MaxLength(200)]
        [Display(Name = "Contact Person")]
        public string? ContactPerson { get; set; }

        [MaxLength(200)]
        [EmailAddress]
        [Display(Name = "Contact Email")]
        public string? ContactEmail { get; set; }

        [MaxLength(50)]
        [Display(Name = "Contact Phone")]
        public string? ContactPhone { get; set; }

        [Display(Name = "FBR Security Token")]
        public string? FbrSecurityToken { get; set; }

        [Display(Name = "Token Expiry Date & Time")]
        [DataType(DataType.DateTime)]
        public DateTime? TokenExpiryDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Is Blocked")]
        public bool IsBlocked { get; set; }

        [MaxLength(500)]
        [Display(Name = "Block Reason")]
        public string? BlockReason { get; set; }

        [Display(Name = "Subscription Start Date & Time")]
        [DataType(DataType.DateTime)]
        public DateTime? SubscriptionStartDate { get; set; }

        [Display(Name = "Subscription End Date & Time")]
        [DataType(DataType.DateTime)]
        public DateTime? SubscriptionEndDate { get; set; }

        [Display(Name = "Maximum Users")]
        [Range(1, int.MaxValue, ErrorMessage = "Max Users must be at least 1")]
        public int? MaxUsers { get; set; }

        [MaxLength(500)]
        [Display(Name = "Logo URL")]
        public string? LogoUrl { get; set; }

        [Display(Name = "Created Date")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "Created By")]
        public int? CreatedBy { get; set; }

        [Display(Name = "Updated Date")]
        [DataType(DataType.DateTime)]
        public DateTime? UpdatedDate { get; set; }

        [Display(Name = "Updated By")]
        public int? UpdatedBy { get; set; }

        public bool IsTwoFactorEnabled { get; set; } = false;

        [MaxLength(1000)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual SubscriptionSetting? SubscriptionSetting { get; set; }
        public virtual ICollection<Buyer> Buyers { get; set; } = new List<Buyer>();

        [NotMapped]
        public string SubscriptionName => !string.IsNullOrEmpty(BusinessName)
            ? BusinessName
            : CompanyName;

        // Helper property to format dates for display
        [NotMapped]
        public string FormattedStartDate => SubscriptionStartDate?.ToString("dd MMM yyyy hh:mm tt") ?? "N/A";

        [NotMapped]
        public string FormattedEndDate => SubscriptionEndDate?.ToString("dd MMM yyyy hh:mm tt") ?? "N/A";

        [NotMapped]
        public string FormattedTokenExpiry => TokenExpiryDate?.ToString("dd MMM yyyy hh:mm tt") ?? "N/A";
    }
}