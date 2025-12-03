using System;
using System.ComponentModel.DataAnnotations;

namespace OctoDI.Web.Models.DatabaseModels
{
    public class GlobalSaleRate
    {
        [Key]
        public int GlobalSaleRateId { get; set; }

        [Required]
        public string? ProductDescription { get; set; }

        public string? HSCode { get; set; }

        [Required]
        public int UnitId { get; set; }
        public Unit? Unit { get; set; }

        [Required]
        public int ServiceCategoryId { get; set; }
        public ServiceCategory? ServiceCategory { get; set; }

        [Required]
        public decimal Rate { get; set; }

        [Required]
        public decimal TaxRate { get; set; }

        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
    }
}