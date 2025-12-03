// File: Models/ViewModels/SubscriptionAdminDashboardViewModel.cs
using System;

namespace OctoDI.Web.Models.ViewModels
{
    public class SubscriptionAdminDashboardViewModel
    {
        public int SubscriptionId { get; set; }
        public string SubscriptionName { get; set; }
        public bool IsActive { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime? CreatedDate { get; set; }

        // Statistics
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalInvoices { get; set; }
        public int PendingInvoices { get; set; }
        public int TotalBuyers { get; set; }
        public int ActiveBuyers { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }  // Expiry date of subscription
        public DateTime? TokenExpiryDate { get; set; }
        // FBR Info
        public bool isActive { get; set; }
        public DateTime? LastFbrSync { get; set; }
    }
}