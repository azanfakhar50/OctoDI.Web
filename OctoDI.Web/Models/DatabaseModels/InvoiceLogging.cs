using System;
using System.ComponentModel.DataAnnotations;

namespace OctoDI.Web.Models.DatabaseModels
{
    // InvoiceLogs.cs
    public class InvoiceLog
    {
        public int InvoiceLogId { get; set; } // ✅ Primary Key
        public int InvoiceId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public string SessionId { get; set; }
        public int? SubscriptionId { get; set; }
        public string CompanyName { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }

        public ICollection<InvoiceRequestLog> RequestLogs { get; set; }
        public ICollection<InvoiceResponseLog> ResponseLogs { get; set; }
    }

    public class InvoiceRequestLog
    {
        public int InvoiceRequestLogId { get; set; } // ✅ Primary Key
        public int InvoiceLogId { get; set; }
        public InvoiceLog InvoiceLog { get; set; }

        public string RequestPayload { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
    }

    public class InvoiceResponseLog
    {
        public int InvoiceResponseLogId { get; set; } // ✅ Primary Key
        public int InvoiceLogId { get; set; }
        public InvoiceLog InvoiceLog { get; set; }

        public string ResponsePayload { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
    }



}
