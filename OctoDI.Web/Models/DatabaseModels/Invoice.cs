using System;
using System.Collections.Generic;

namespace OctoDI.Web.Models.DatabaseModels
{
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public int SubscriptionId { get; set; }
        public string? InvoiceType { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int? BuyerId { get; set; }
        public string? InvoiceRefNo { get; set; }
        public string? FBRInvoiceNo { get; set; }
        public string? Status { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string?  Remarks { get; set; }

        public Buyer? Buyer { get; set; }
        public ICollection<InvoiceItem>? Items { get; set; }
        public virtual Subscription? Subscription { get; set; }
    }
}
