using Microsoft.AspNetCore.Http;
using OctoDI.Web.Models.DatabaseModels;
using System;
using System.Threading.Tasks;

public class InvoiceLogService : IInvoiceLogService
{
    private readonly InvoiceLoggingDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InvoiceLogService(InvoiceLoggingDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogInvoiceAsync(
        int invoiceId,
        int userId,
        string username,
        string requestPayload,
        string responsePayload,
        string status,
        int? subscriptionId = null,
        string companyName = null
    )
    {
        var sessionId = _httpContextAccessor.HttpContext?.Session?.Id;

        // 1️⃣ Create main log
        var log = new InvoiceLog
        {
            InvoiceId = invoiceId,
            UserId = userId,
            Username = username,
            SubscriptionId = subscriptionId,
            CompanyName = companyName,
            SessionId = sessionId,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };
        _context.InvoiceLogs.Add(log);
        await _context.SaveChangesAsync(); // Save to get InvoiceLogId

        // 2️⃣ Add Request log
        if (!string.IsNullOrEmpty(requestPayload))
        {
            var requestLog = new InvoiceRequestLog
            {
                InvoiceLogId = log.InvoiceLogId,
                RequestPayload = requestPayload,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.InvoiceRequestLogs.Add(requestLog);
        }

        // 3️⃣ Add Response log
        if (!string.IsNullOrEmpty(responsePayload))
        {
            var responseLog = new InvoiceResponseLog
            {
                InvoiceLogId = log.InvoiceLogId,
                ResponsePayload = responsePayload,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.InvoiceResponseLogs.Add(responseLog);
        }

        await _context.SaveChangesAsync();
    }
}
