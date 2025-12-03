using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OctoDI.Web.Models.ViewModels
{
    public class InvoiceCreateVM
    {
        public int SubscriptionId { get; set; }

        [Required]
        public string InvoiceType { get; set; }
        public string CompanyName { get; set; }
        public string CNIC { get; set; }
        public string Contact { get; set; }
        public string Province { get; set; }
        public string Address { get; set; }


        [Required]
        public DateTime InvoiceDate { get; set; }

        [Required]
        public int BuyerId { get; set; }

        public string InvoiceRefNo { get; set; }
        public string FBRInvoiceNo { get; set; }
        public string Status { get; set; } = "Draft";
        public string Remarks { get; set; }

        public List<InvoiceItemVM> Items { get; set; } = new();
    }

    public class InvoiceItemVM
    {
        [Required]
        public string ProductDescription { get; set; }
        public string HSCode { get; set; }
        public decimal Rate { get; set; }
        public string UOM { get; set; }
        public decimal Quantity { get; set; }
        public decimal ValueExclST { get; set; }
        public decimal SalesTaxApplicable { get; set; }
        public decimal Discount { get; set; }
    }
}
