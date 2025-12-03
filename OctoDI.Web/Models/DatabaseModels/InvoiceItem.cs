using System;

namespace OctoDI.Web.Models.DatabaseModels
{
    public class InvoiceItem
    {
        public int ItemId { get; set; }
        public int InvoiceId { get; set; }
        public string HSCode { get; set; }
        public string ProductDescription { get; set; }
        public decimal? Rate { get; set; }
        public string UOM { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? ValueExclST { get; set; }
        public decimal? SalesTaxApplicable { get; set; }
        public decimal? FurtherTax { get; set; }
        public decimal? ExtraTax { get; set; }
        public decimal? Discount { get; set; }
        public decimal? SalesTaxAmount { get; set; }
        public decimal? TotalAmount { get; set; }

        public decimal? FEDPayable { get; set; }

        public Invoice Invoice { get; set; }
    }
}
