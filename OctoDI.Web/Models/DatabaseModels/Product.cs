using System;
using System.ComponentModel.DataAnnotations;

namespace OctoDI.Web.Models.DatabaseModels
{
    public class Unit
    {
        [Key]
        public int UnitId { get; set; }
        public string? Name { get; set; }

        public int? SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }
    }

    public class ServiceCategory
    {
        [Key]
        public int ServiceCategoryId { get; set; }
        public string? Name { get; set; }
        public decimal TaxRate { get; set; }

        public int? SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }

        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class Product
    {
        [Key]
        public int ProductId { get; set; }
        public string? HSCode { get; set; }  // NEW}

        public string? ProductDescription { get; set; }

        public int UnitId { get; set; }
        public Unit? Unit { get; set; }

        public int ServiceCategoryId { get; set; }
        public ServiceCategory? ServiceCategory { get; set; }

        public decimal Rate { get; set; }
        public decimal TaxRate { get; set; }

        public int? SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }

        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
    }

}
