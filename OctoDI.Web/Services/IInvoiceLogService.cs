using System.Threading.Tasks;

public interface IInvoiceLogService
{
    Task LogInvoiceAsync(
        int invoiceId,
        int userId,
        string username,
        string requestPayload,
        string responsePayload,
        string status,
        int? subscriptionId = null,
        string companyName = null
    );
}
