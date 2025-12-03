using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OctoDI.Web.Models.DatabaseModels
{
    [Table("FbrLogs")]
    public class FbrLog
    {
        [Key]
        public int LogId { get; set; }

        public int SubscriptionId { get; set; }
        public string? ApiName { get; set; }          
        public string? RequestPayload { get; set; }
        public string? ResponsePayload { get; set; }
        public bool IsSuccess { get; set; }
        public DateTime LogDate { get; set; } = DateTime.Now;
        public string? Remarks { get; set; }

        [ForeignKey("SubscriptionId")]
        public virtual Subscription Subscription { get; set; } = null!;
    }
}
