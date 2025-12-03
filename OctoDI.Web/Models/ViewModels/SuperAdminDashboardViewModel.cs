using System;
using System.Collections.Generic;

namespace OctoDI.Web.Models.ViewModels
{
    public class SuperAdminDashboardViewModel
    {
        public int TotalSubscriptions { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int BlockedSubscriptions { get; set; }

        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }

        public int TotalInvoices { get; set; }
        public int PendingInvoices { get; set; }

        public List<RecentSubscriptionViewModel> RecentSubscriptions { get; set; } = new List<RecentSubscriptionViewModel>();
    }

    public class RecentSubscriptionViewModel
    {
        public int SubscriptionId { get; set; }
        public string CompanyName { get; set; }
        public bool IsActive { get; set; }
        public bool IsBlocked { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalInvoices { get; set; }
        public bool FbrConfigured { get; set; }
    }

    public class InvoiceViewModel
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string SubscriptionName { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public decimal Amount { get; set; }
    }
}
