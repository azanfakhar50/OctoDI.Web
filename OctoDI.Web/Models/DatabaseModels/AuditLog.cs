namespace OctoDI.Web.Models.DatabaseModels
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
        public int? SubscriptionId { get; set; }
        public string CompanyName { get; set; }
        public string Action { get; set; }
        public string IPAddress { get; set; }
        public string UserAgent { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

}
