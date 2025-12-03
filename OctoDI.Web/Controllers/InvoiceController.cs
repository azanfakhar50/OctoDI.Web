using iTextSharp.text;  // <- for Paragraph, Chunk, Phrase, etc.
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OctoDI.Web.Models.DatabaseModels;
using OfficeOpenXml;
using System;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace OctoDI.Web.Controllers
{
    [Authorize]
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly IInvoiceLogService _invoiceLogService;
        public InvoiceController(ApplicationDbContext context,IInvoiceLogService invoiceLogService)
        {
            _context = context;
            _invoiceLogService = invoiceLogService;
        }

        public async Task<IActionResult> Index(string status = null, string search = null, int page = 1, int pageSize = 10)
        {
            // ==================== GET USER INFO FROM CLAIMS ====================
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "SubscriptionUser";
            var currentUserName = User.Identity?.Name ?? "";

            var userIdClaim = User.FindFirst("UserId")?.Value;
            int currentUserId = 0;
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                int.TryParse(userIdClaim, out currentUserId);
            }

            var subscriptionIdClaim = User.FindFirst("SubscriptionId")?.Value;
            int subscriptionId = 0;
            if (!string.IsNullOrEmpty(subscriptionIdClaim))
            {
                int.TryParse(subscriptionIdClaim, out subscriptionId);
            }

            Console.WriteLine($"========== INVOICE INDEX - ROLE FILTERING ==========");
            Console.WriteLine($"👤 User: {currentUserName} | Role: {role} | UserId: {currentUserId} | SubscriptionId: {subscriptionId}");

            // ==================== BASE QUERY - DIRECT DB DATA ====================
            IQueryable<Invoice> query;

            // ==================== ROLE-BASED FILTER ====================
            if (role == "SuperAdmin")
            {
                Console.WriteLine("🔓 SuperAdmin: Viewing ALL invoices");
                query = _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Items)  // ✅ Direct items from DB
                    .Include(i => i.Subscription)
                    .AsNoTracking();  // ✅ Read-only, no tracking
            }
            else if (role == "SubscriptionAdmin")
            {
                Console.WriteLine($"🔐 SubscriptionAdmin: Viewing own invoices + invoices from users created by UserId {currentUserId}");

                var usersCreatedByAdmin = await _context.Users
                    .Where(u => u.CreatedBy == currentUserId && u.IsActive)
                    .Select(u => u.Username)
                    .ToListAsync();

                Console.WriteLine($"📋 Found {usersCreatedByAdmin.Count} users created by this admin");

                query = _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Items)  // ✅ Direct items from DB
                    .Include(i => i.Subscription)
                    .Where(i => i.SubscriptionId == subscriptionId &&
                               (i.CreatedBy == currentUserName || usersCreatedByAdmin.Contains(i.CreatedBy)))
                    .AsNoTracking();  // ✅ Read-only
            }
            else // SubscriptionUser
            {
                Console.WriteLine($"🔒 SubscriptionUser: Viewing only own invoices (CreatedBy = {currentUserName})");

                query = _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Items)  // ✅ Direct items from DB
                    .Include(i => i.Subscription)
                    .Where(i => i.SubscriptionId == subscriptionId && i.CreatedBy == currentUserName)
                    .AsNoTracking();  // ✅ Read-only
            }

            // ==================== FILTER BY STATUS & SEARCH ====================
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.Status == status);
                Console.WriteLine($"🔍 Filtering by status: {status}");
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(i =>
                    (i.InvoiceRefNo ?? "").Contains(search) ||
                    (i.FBRInvoiceNo ?? "").Contains(search) ||
                    (i.Buyer != null ? i.Buyer.BuyerBusinessName : "").Contains(search));
                Console.WriteLine($"🔍 Searching for: {search}");
            }

            // ==================== PAGINATION ====================
            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            // ✅ Direct fetch from database - NO calculations here
            var invoices = await query
                .OrderByDescending(i => i.InvoiceDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Console.WriteLine($"📊 Total Records: {totalRecords} | Page {page}/{totalPages}");

            // ==================== VIEWBAG ====================
            ViewBag.PageNumber = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.CurrentStatus = status;
            ViewBag.SearchTerm = search;
            ViewBag.UserRole = role;

            // ✅ Return raw data from database
            return View(invoices);
        }

        // ==================== CREATE INVOICE - GET ====================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var subscriptionClaim = User.FindFirst("SubscriptionId")?.Value;
            if (string.IsNullOrEmpty(subscriptionClaim))
                return Unauthorized();

            int subscriptionId = int.Parse(subscriptionClaim);

            // Get subscription type from claims
            var subscriptionType = User.FindFirst("SubscriptionType")?.Value ?? "Goods";

            // Load buyers for this subscription
            var buyers = await _context.Buyers
                .Where(b => b.SubscriptionId == subscriptionId)
                .OrderBy(b => b.BuyerBusinessName)
                .ToListAsync();

            // ==================== LOAD PRODUCTS ====================
            // 1️⃣ SuperAdmin global products (SubscriptionId == null)
            var globalProducts = await _context.Products
                .Include(p => p.Unit)
                .Include(p => p.ServiceCategory)
                .Where(p => p.SubscriptionId == null)
                .ToListAsync();

            // 2️⃣ SubscriptionAdmin products (current subscription)
            var subscriptionProducts = await _context.Products
                .Include(p => p.Unit)
                .Include(p => p.ServiceCategory)
                .Where(p => p.SubscriptionId == subscriptionId)
                .ToListAsync();

            // 3️⃣ Merge products: subscription products override global products
            var productsDict = globalProducts.ToDictionary(p => p.ProductId, p => p);
            foreach (var subProd in subscriptionProducts)
            {
                productsDict[subProd.ProductId] = subProd; // override global
            }

            var finalProducts = productsDict.Values
                .OrderBy(p => p.ProductDescription)
                .ToList();

            // ==================== VIEWBAG ====================
            ViewBag.SubscriptionId = subscriptionId;
            ViewBag.SubscriptionType = subscriptionType;
            ViewBag.Buyers = buyers;
            ViewBag.Products = finalProducts; // merged product list

            // ==================== MODEL ====================
            var model = new Invoice
            {
                SubscriptionId = subscriptionId,
                InvoiceDate = DateTime.Now,
                Status = "Draft"
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice model)
        {
            var subscriptionClaim = User.FindFirst("SubscriptionId")?.Value;
            if (string.IsNullOrEmpty(subscriptionClaim))
                return Unauthorized();

            int subscriptionId = int.Parse(subscriptionClaim);
            model.SubscriptionId = subscriptionId;

            // ✅ LOAD SUBSCRIPTION DATA
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (subscription == null)
            {
                TempData["Error"] = "Subscription not found.";
                return RedirectToAction("Index");
            }

            ModelState.Remove("Buyer");
            ModelState.Remove("Items");

            if (model.Items == null || !model.Items.Any())
            {
                TempData["Error"] = "Please add at least one invoice item.";
                ViewBag.SubscriptionId = subscriptionId;
                ViewBag.SubscriptionType = User.FindFirst("SubscriptionType")?.Value ?? "Goods";
                ViewBag.Buyers = await _context.Buyers.Where(b => b.SubscriptionId == subscriptionId).ToListAsync();
                ViewBag.Products = await _context.Products
                    .Include(p => p.Unit)
                    .Include(p => p.ServiceCategory)
                    .Where(p => p.SubscriptionId == subscriptionId)
                    .ToListAsync();
                return View(model);
            }

            // ==================== CALCULATIONS ====================
            foreach (var item in model.Items)
            {
                item.ValueExclST = item.Rate * item.Quantity;
                decimal taxRate = item.SalesTaxApplicable ?? 0;
                item.SalesTaxAmount = taxRate;
                decimal discountAmount = item.Discount ?? 0;
                item.TotalAmount = item.ValueExclST + item.SalesTaxAmount - discountAmount;
            }

            model.InvoiceRefNo = await GenerateInvoiceRefNo(subscriptionId);
            model.CreatedDate = DateTime.Now;
            model.CreatedBy = User.Identity?.Name ?? "System";

            _context.Invoices.Add(model);
            await _context.SaveChangesAsync();

            // ==================== LOGGING TO JSON ====================
            try
            {
                var requestPayload = JsonConvert.SerializeObject(model, Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    });

                var responsePayload = $"Invoice {model.InvoiceRefNo} created successfully";

                // ✅ SAFELY GET USER DATA
                var currentUserId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
                var currentUserName = User.Identity?.Name ?? "System";

                // ✅ USE LOADED SUBSCRIPTION DATA
                await _invoiceLogService.LogInvoiceAsync(
                    invoiceId: model.InvoiceId,
                    userId: currentUserId,
                    username: currentUserName,
                    requestPayload: requestPayload,
                    responsePayload: responsePayload,
                    status: "Created",
                    subscriptionId: subscriptionId,
                    companyName: subscription.CompanyName  // ✅ NOW IT'S NOT NULL
                );

                Console.WriteLine($"✅ Invoice logged successfully: {model.InvoiceRefNo}");
            }
            catch (Exception ex)
            {
                // ✅ LOG THE FULL ERROR
                Console.WriteLine($"❌ InvoiceLogService Error: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");

                // ✅ LOG INNER EXCEPTION IF EXISTS
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                }
            }

            bool sentToFbr = await CallFBRApiAndUpdateInvoice(model.InvoiceId);

            TempData["Success"] = sentToFbr
                ? $"Invoice {model.InvoiceRefNo} created and posted to FBR successfully!"
                : $"Invoice {model.InvoiceRefNo} saved locally but FBR API failed.";

            return RedirectToAction("Details", new { id = model.InvoiceId });
        }



        // ==================== VIEW INVOICE DETAILS ====================
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Buyer)
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null)
            {
                TempData["Error"] = "Invoice not found!";
                return RedirectToAction("Index");
            }

            ViewBag.Subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.SubscriptionId == invoice.SubscriptionId);

            return View(invoice);
        }



        // ==================== DELETE INVOICE ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice != null)
            {
                _context.InvoiceItems.RemoveRange(invoice.Items);
                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Invoice deleted successfully!";
            }

            return RedirectToAction("Index");
        }

        // ==================== GENERATE INVOICE REF NO ====================
        private async Task<string> GenerateInvoiceRefNo(int subscriptionId)
        {
            var lastInvoice = await _context.Invoices
                .Where(i => i.SubscriptionId == subscriptionId)
                .OrderByDescending(i => i.InvoiceId)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastInvoice != null && !string.IsNullOrEmpty(lastInvoice.InvoiceRefNo))
            {
                var parts = lastInvoice.InvoiceRefNo.Split('-');
                if (parts.Length >= 3 && int.TryParse(parts[^1], out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"OCT-{DateTime.Now:yyMMdd}-{nextNumber:D3}";
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendToFBR(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Items)
                    .Include(i => i.Buyer)
                    .FirstOrDefaultAsync(i => i.InvoiceId == id);

                if (invoice == null)
                    return Json(new { success = false, message = "Invoice not found!" });

                if (!string.IsNullOrEmpty(invoice.FBRInvoiceNo))
                    return Json(new { success = false, message = "Invoice already has FBR Invoice Number!" });

                bool result = await CallFBRApiAndUpdateInvoice(invoice.InvoiceId);

                invoice = await _context.Invoices.FindAsync(id);

                return result
                    ? Json(new { success = true, message = "Invoice successfully sent to FBR!", fbrInvoiceNo = invoice.FBRInvoiceNo, status = invoice.Status })
                    : Json(new { success = false, message = $"FBR API failed: {invoice.Remarks}" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ResendToFBR Error: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .Include(i => i.Buyer)
                .Include(i => i.Subscription)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null)
                return NotFound();

            // Generate enhanced PDF using the helper method
            var pdfBytes = GenerateInvoicePdfBytes(invoice);

            return File(pdfBytes, "application/pdf", $"Invoice_{invoice.InvoiceRefNo}.pdf");
        }
        // ==================== ENHANCED PDF GENERATION ====================
        private byte[] GenerateInvoicePdfBytes(Invoice invoice)
        {
            using (var stream = new MemoryStream())
            {
                var document = new iTextSharp.text.Document(PageSize.A4, 40, 40, 60, 60);
                PdfWriter writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                // ==================== FONTS ====================
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, new BaseColor(64, 64, 64));
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(255, 255, 255));
                var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(0, 0, 0));
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, new BaseColor(0, 0, 0));
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(128, 128, 128));

                // ==================== HEADER - E-INVOICING ====================
                var headerTable = new PdfPTable(2) { WidthPercentage = 100 };
                headerTable.SetWidths(new float[] { 50, 50 });

                // Left: Company Logo/Name
                var leftCell = new PdfPCell();
                leftCell.Border = Rectangle.NO_BORDER;
                leftCell.AddElement(new Paragraph("E-INVOICING SYSTEM", titleFont));
                leftCell.AddElement(new Paragraph(" ", normalFont));
                leftCell.AddElement(new Paragraph($"Invoice Date: {invoice.InvoiceDate.ToString("dd MMM yyyy")}", normalFont));
                leftCell.AddElement(new Paragraph($"Invoice Type: {invoice.InvoiceType}", normalFont));
                headerTable.AddCell(leftCell);

                // Right: Invoice Numbers
                var rightCell = new PdfPCell();
                rightCell.Border = Rectangle.NO_BORDER;
                rightCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                rightCell.AddElement(new Paragraph($"Invoice #: {invoice.InvoiceRefNo}", boldFont));
                if (!string.IsNullOrEmpty(invoice.FBRInvoiceNo))
                {
                    rightCell.AddElement(new Paragraph($"FBR Invoice #: {invoice.FBRInvoiceNo}", boldFont));
                }
                rightCell.AddElement(new Paragraph($"Status: {invoice.Status}", normalFont));
                headerTable.AddCell(rightCell);

                document.Add(headerTable);
                document.Add(new Paragraph(" "));

                // ==================== SEPARATOR LINE ====================
                var line = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, new BaseColor(211, 211, 211), Element.ALIGN_CENTER, -2);
                document.Add(line);
                document.Add(new Paragraph(" "));

                // ==================== SELLER & BUYER INFO ====================
                var infoTable = new PdfPTable(2) { WidthPercentage = 100 };
                infoTable.SetWidths(new float[] { 50, 50 });
                infoTable.DefaultCell.Border = Rectangle.BOX;
                infoTable.DefaultCell.Padding = 10;
                infoTable.DefaultCell.BackgroundColor = new BaseColor(245, 245, 245);

                // SELLER INFO
                var sellerCell = new PdfPCell();
                sellerCell.Padding = 10;
                sellerCell.BackgroundColor = new BaseColor(230, 240, 255);
                sellerCell.AddElement(new Paragraph("SELLER INFORMATION", headerFont));
                sellerCell.AddElement(new Paragraph(" ", normalFont));
                sellerCell.AddElement(new Paragraph($"Company: {invoice.Subscription?.CompanyName ?? "N/A"}", boldFont));

                // Get NTN from SubscriptionSettings if available
                var sellerNTN = "N/A";
                var sellerPhone = "N/A";
                var sellerAddress = invoice.Subscription?.Address ?? "N/A";

                var settings = _context.SubscriptionSettings
                    .FirstOrDefault(s => s.SubscriptionId == invoice.SubscriptionId && s.IsActive);

                if (settings != null)
                {
                    sellerNTN = settings.SellerNTN ?? "N/A";
                    sellerAddress = settings.SellerAddress ?? sellerAddress;
                }

                sellerCell.AddElement(new Paragraph($"NTN: {sellerNTN}", normalFont));
                sellerCell.AddElement(new Paragraph($"Address: {sellerAddress}", normalFont));
                sellerCell.AddElement(new Paragraph($"Phone: {sellerPhone}", normalFont));
                infoTable.AddCell(sellerCell);

                // BUYER INFO
                var buyerCell = new PdfPCell();
                buyerCell.Padding = 10;
                buyerCell.BackgroundColor = new BaseColor(255, 245, 230);
                buyerCell.AddElement(new Paragraph("BUYER INFORMATION", headerFont));
                buyerCell.AddElement(new Paragraph(" ", normalFont));
                buyerCell.AddElement(new Paragraph($"Business Name: {invoice.Buyer?.BuyerBusinessName ?? "N/A"}", boldFont));
                buyerCell.AddElement(new Paragraph($"NTN/CNIC: {invoice.Buyer?.BuyerNTN ?? "N/A"}", normalFont));
                buyerCell.AddElement(new Paragraph($"Address: {invoice.Buyer?.BuyerAddress ?? "N/A"}", normalFont));
                buyerCell.AddElement(new Paragraph($"Province: {invoice.Buyer?.BuyerProvince ?? "N/A"}", normalFont));
                buyerCell.AddElement(new Paragraph($"Type: {invoice.Buyer?.BuyerRegistrationType ?? "N/A"}", normalFont));
                infoTable.AddCell(buyerCell);

                document.Add(infoTable);
                document.Add(new Paragraph(" "));

                // ==================== ITEMS TABLE ====================
                var itemsTable = new PdfPTable(7) { WidthPercentage = 100 };
                itemsTable.SetWidths(new float[] { 5, 25, 10, 10, 12, 12, 15 });

                // Header Row
                var headerBg = new BaseColor(70, 130, 180);
                string[] headers = { "Sr", "Product ", "HS Code", "Qty", "Rate (Rs.)", "Tax Applied", "Total (Rs.)" };
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = headerBg;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Padding = 8;
                    itemsTable.AddCell(cell);
                }

                // Items Data
                int itemNo = 1;
                decimal grandTotal = 0;
                decimal totalTax = 0;
                decimal subTotal = 0;

                foreach (var item in invoice.Items ?? new List<InvoiceItem>())
                {
                    var rowBg = new BaseColor(250, 250, 250);

                    itemsTable.AddCell(new PdfPCell(new Phrase(itemNo.ToString(), normalFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5,
                        BackgroundColor = rowBg
                    });

                    itemsTable.AddCell(new PdfPCell(new Phrase(item.ProductDescription ?? "", normalFont))
                    {
                        Padding = 5,
                        BackgroundColor = rowBg
                    });

                    itemsTable.AddCell(new PdfPCell(new Phrase(item.HSCode ?? "", normalFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5,
                        BackgroundColor = rowBg
                    });

                    itemsTable.AddCell(new PdfPCell(new Phrase(item.Quantity.ToString(), normalFont))
                    {
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        Padding = 5,
                        BackgroundColor = rowBg
                    });

                    decimal rateValue = item.Rate ?? 0;
                    itemsTable.AddCell(new PdfPCell(new Phrase(rateValue.ToString("N2"), normalFont))
                    {
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        Padding = 5,
                        BackgroundColor = rowBg
                    });

                    decimal taxValue = item.SalesTaxApplicable ?? 0;
                    itemsTable.AddCell(new PdfPCell(new Phrase(taxValue.ToString("N1") + "%", normalFont))
                    {
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        Padding = 5,
                        BackgroundColor = rowBg
                    });

                    decimal totalValue = item.TotalAmount ?? 0;
                    itemsTable.AddCell(new PdfPCell(new Phrase(totalValue.ToString("N2"), boldFont))
                    {
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        Padding = 5,
                        BackgroundColor = rowBg
                    });

                    subTotal += item.ValueExclST ?? 0;
                    totalTax += item.SalesTaxAmount ?? 0;
                    grandTotal += item.TotalAmount ?? 0;
                    itemNo++;
                }

                document.Add(itemsTable);
                document.Add(new Paragraph(" "));

                // ==================== SUMMARY TABLE ====================
                var summaryTable = new PdfPTable(2) { WidthPercentage = 40, HorizontalAlignment = Element.ALIGN_RIGHT };
                summaryTable.SetWidths(new float[] { 60, 40 });

                summaryTable.AddCell(new PdfPCell(new Phrase("Subtotal:", boldFont))
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 5
                });
                summaryTable.AddCell(new PdfPCell(new Phrase("Rs. " + subTotal.ToString("N2"), normalFont))
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 5
                });

                summaryTable.AddCell(new PdfPCell(new Phrase("Total Tax:", boldFont))
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 5
                });
                summaryTable.AddCell(new PdfPCell(new Phrase("Rs. " + totalTax.ToString("N2"), normalFont))
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 5
                });

                var grandTotalBg = new BaseColor(70, 130, 180);

                summaryTable.AddCell(new PdfPCell(new Phrase("Grand Total:", headerFont))
                {
                    BackgroundColor = grandTotalBg,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 8
                });
                summaryTable.AddCell(new PdfPCell(new Phrase("Rs. " + grandTotal.ToString("N2"), headerFont))
                {
                    BackgroundColor = grandTotalBg,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 8
                });

                document.Add(summaryTable);
                document.Add(new Paragraph(" "));
                document.Add(new Paragraph(" "));

                // ==================== FOOTER ====================
                document.Add(line);
                var footer = new Paragraph($"Created by: {invoice.CreatedBy ?? "System"} | Generated on: {DateTime.Now.ToString("dd MMM yyyy hh:mm tt")}", smallFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);

                document.Close();
                return stream.ToArray();
            }
        }

        public async Task<IActionResult> DownloadExcel(int id)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .Include(i => i.Buyer)
                .Include(i => i.Subscription)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null)
                return NotFound();

            try
            {
                using (ExcelPackage package = new ExcelPackage())
                {
                    var sheet = package.Workbook.Worksheets.Add("Invoice");
                    sheet.Cells[1, 1].Value = "Invoice Ref No";
                    sheet.Cells[1, 2].Value = "FBR Invoice No";
                    sheet.Cells[1, 3].Value = "Invoice Date";
                    sheet.Cells[1, 4].Value = "Invoice Type";
                    sheet.Cells[1, 5].Value = "Status";
                    sheet.Cells[1, 6].Value = "Buyer Name";
                    sheet.Cells[1, 7].Value = "Buyer NTN";
                    sheet.Cells[1, 8].Value = "Product Description";
                    sheet.Cells[1, 9].Value = "HS Code";
                    sheet.Cells[1, 10].Value = "Quantity";
                    sheet.Cells[1, 11].Value = "Rate";
                    sheet.Cells[1, 12].Value = "Tax %";
                    sheet.Cells[1, 13].Value = "Total Amount";

                    sheet.Row(1).Style.Font.Bold = true;

                    // ==================== DATA ROWS ====================
                    int row = 2;

                    foreach (var item in invoice.Items ?? new List<InvoiceItem>())
                    {
                        sheet.Cells[row, 1].Value = invoice.InvoiceRefNo;
                        sheet.Cells[row, 2].Value = invoice.FBRInvoiceNo ?? "";
                        sheet.Cells[row, 3].Value = invoice.InvoiceDate.ToString("yyyy-MM-dd");
                        sheet.Cells[row, 4].Value = invoice.InvoiceType;
                        sheet.Cells[row, 5].Value = invoice.Status;
                        sheet.Cells[row, 6].Value = invoice.Buyer?.BuyerBusinessName ?? "";
                        sheet.Cells[row, 7].Value = invoice.Buyer?.BuyerNTN ?? "";
                        sheet.Cells[row, 8].Value = item.ProductDescription;
                        sheet.Cells[row, 9].Value = item.HSCode;
                        sheet.Cells[row, 10].Value = item.Quantity;
                        sheet.Cells[row, 11].Value = item.Rate;
                        sheet.Cells[row, 12].Value = item.SalesTaxApplicable ?? 0;
                        sheet.Cells[row, 13].Value = item.TotalAmount ?? 0;

                        // Format numbers
                        sheet.Cells[row, 11].Style.Numberformat.Format = "#,##0.00";
                        sheet.Cells[row, 13].Style.Numberformat.Format = "#,##0.00";

                        row++;
                    }

                    // Auto-fit all columns
                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

                    // Return file
                    var fileBytes = package.GetAsByteArray();
                    return File(fileBytes,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Invoice_{invoice.InvoiceRefNo}.xlsx");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excel Download Error: {ex.Message}");
                TempData["Error"] = $"Error generating Excel: {ex.Message}";
                return RedirectToAction("Details", new { id = invoice.InvoiceId });
            }
        }

        /// <summary>
        /// Downloads multiple selected invoices into a single Excel file.
        /// </summary>
        /// <param name="ids">Comma-separated string of InvoiceIds to download.</param>
        /// <returns>Excel file containing all selected invoice data.</returns>
        public async Task<IActionResult> DownloadBulkExcel(string ids)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            if (string.IsNullOrEmpty(ids))
            {
                TempData["Error"] = "No invoices selected for bulk download.";
                return RedirectToAction("Index");
            }

            // 1. Get the list of IDs
            var idList = ids.Split(',')
                            .Select(s => int.TryParse(s.Trim(), out int id) ? id : (int?)null)
                            .Where(id => id.HasValue)
                            .Select(id => id.Value)
                            .ToList();

            if (!idList.Any())
            {
                TempData["Error"] = "Invalid invoice selection.";
                return RedirectToAction("Index");
            }

            // 2. Fetch all selected Invoices from the database
            var invoices = await _context.Invoices
                .Include(i => i.Items)
                .Include(i => i.Buyer)
                .Where(i => idList.Contains(i.InvoiceId)) // Filter by the selected IDs
                .AsNoTracking() // Read-only data for better performance
                .ToListAsync();

            if (!invoices.Any())
            {
                TempData["Error"] = "Selected invoices not found.";
                return RedirectToAction("Index");
            }

            try
            {
                using (ExcelPackage package = new ExcelPackage())
                {
                    var sheet = package.Workbook.Worksheets.Add("Invoices_Bulk_Data");

                    // 3. Define the Header Row (Same as single download, but in bulk context)
                    int col = 1;
                    sheet.Cells[1, col++].Value = "InvoiceID";
                    sheet.Cells[1, col++].Value = "Invoice Ref No";
                    sheet.Cells[1, col++].Value = "FBR Invoice No";
                    sheet.Cells[1, col++].Value = "Invoice Date";
                    sheet.Cells[1, col++].Value = "Invoice Type";
                    sheet.Cells[1, col++].Value = "Status";
                    sheet.Cells[1, col++].Value = "Buyer Name";
                    sheet.Cells[1, col++].Value = "Buyer NTN";
                    sheet.Cells[1, col++].Value = "Product Description";
                    sheet.Cells[1, col++].Value = "HS Code";
                    sheet.Cells[1, col++].Value = "Quantity";
                    sheet.Cells[1, col++].Value = "Rate";
                    sheet.Cells[1, col++].Value = "Tax %";
                    sheet.Cells[1, col++].Value = "Tax Amount"; // Added Tax Amount for clarity
                    sheet.Cells[1, col++].Value = "Total Amount"; // Total per item

                    sheet.Row(1).Style.Font.Bold = true;
                    sheet.Row(1).Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    sheet.Row(1).Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);


                    // 4. Populate Data Rows
                    int row = 2;

                    foreach (var invoice in invoices)
                    {
                        foreach (var item in invoice.Items ?? new List<InvoiceItem>())
                        {
                            col = 1; // Reset column for each new row
                            sheet.Cells[row, col++].Value = invoice.InvoiceId; // Use Invoice ID for unique reference
                            sheet.Cells[row, col++].Value = invoice.InvoiceRefNo;
                            sheet.Cells[row, col++].Value = invoice.FBRInvoiceNo ?? "";
                            sheet.Cells[row, col++].Value = invoice.InvoiceDate.ToString("yyyy-MM-dd");
                            sheet.Cells[row, col++].Value = invoice.InvoiceType;
                            sheet.Cells[row, col++].Value = invoice.Status;
                            sheet.Cells[row, col++].Value = invoice.Buyer?.BuyerBusinessName ?? "";
                            sheet.Cells[row, col++].Value = invoice.Buyer?.BuyerNTN ?? "";
                            sheet.Cells[row, col++].Value = item.ProductDescription;
                            sheet.Cells[row, col++].Value = item.HSCode;
                            sheet.Cells[row, col++].Value = item.Quantity;
                            sheet.Cells[row, col++].Value = item.Rate;
                            sheet.Cells[row, col++].Value = item.SalesTaxApplicable ?? 0;
                            sheet.Cells[row, col++].Value = item.SalesTaxAmount ?? 0; // Tax Amount
                            sheet.Cells[row, col++].Value = item.TotalAmount ?? 0;

                            // Formatting
                            sheet.Cells[row, 11, row, 15].Style.Numberformat.Format = "#,##0.00"; // Rates and Amounts

                            row++;
                        }
                    }

                    // 5. Auto-fit columns and return file
                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

                    var fileBytes = package.GetAsByteArray();
                    return File(fileBytes,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Bulk_Invoices_Download_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bulk Excel Download Error: {ex.Message}");
                TempData["Error"] = $"Error generating Bulk Excel: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
        public async Task<IActionResult> DownloadBulk(string ids)
        {
            var idList = ids.Split(',').Select(int.Parse).ToList();

            using (var zipStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var id in idList)
                    {
                        // ✅ ADD .Include(i => i.Subscription)
                        var invoice = await _context.Invoices
                            .Include(i => i.Items)
                            .Include(i => i.Buyer)
                            .Include(i => i.Subscription)  // ← THIS WAS MISSING
                            .FirstOrDefaultAsync(i => i.InvoiceId == id);

                        if (invoice == null)
                            continue;

                        var file = archive.CreateEntry($"Invoice_{invoice.InvoiceRefNo}.pdf");

                        using (var entryStream = file.Open())
                        {
                            // Generate PDF content in memory
                            var pdfBytes = GenerateInvoicePdfBytes(invoice);
                            entryStream.Write(pdfBytes, 0, pdfBytes.Length);
                        }
                    }
                }

                return File(zipStream.ToArray(),
                    "application/zip",
                    "Invoices.zip");
            }
        }

        // ==================== FBR API INTEGRATION ====================
        private async Task<bool> CallFBRApiAndUpdateInvoice(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .Include(i => i.Buyer)
                .Include(i => i.Subscription)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null) return false;

            var settings = await _context.SubscriptionSettings
                .FirstOrDefaultAsync(s => s.SubscriptionId == invoice.SubscriptionId && s.IsActive);

            if (settings == null)
            {
                invoice.Status = "FBR_Failed";
                invoice.Remarks = "FBR settings not configured!";
                await _context.SaveChangesAsync();
                return false;
            }

            var payload = new
            {
                invoiceType = invoice.InvoiceType,
                invoiceDate = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                sellerNTNCNIC = settings.SellerNTN ?? "",
                sellerBusinessName = settings.SellerBusinessName ?? "",
                sellerProvince = settings.SellerProvince ?? "Unknown",
                sellerAddress = settings.SellerAddress ?? "N/A",
                buyerNTNCNIC = invoice.Buyer?.BuyerNTN ?? "0000000-0",
                buyerBusinessName = invoice.Buyer?.BuyerBusinessName ?? "Unknown Buyer",
                buyerProvince = invoice.Buyer?.BuyerProvince ?? "Unknown",
                buyerAddress = invoice.Buyer?.BuyerAddress ?? "N/A",
                buyerRegistrationType = invoice.Buyer?.BuyerRegistrationType ?? "Unregistered",
                invoiceRefNo = invoice.InvoiceRefNo,
                items = invoice.Items.Select(i => new
                {
                    hsCode = i.HSCode,
                    productDescription = i.ProductDescription,
                    rate = i.Rate,
                    quantity = i.Quantity,
                    valueSalesExcludingST = i.ValueExclST,
                    salesTaxApplicable = i.SalesTaxApplicable,
                    discount = i.Discount
                }).ToList()
            };

            var jsonData = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            string apiUrl = settings.FbrBaseUrl?.TrimEnd('/') + "/postinvoicedata";

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = content
            };

            if (!string.IsNullOrEmpty(settings.FbrToken))
                request.Headers.Add("Authorization", $"Bearer {settings.FbrToken}");

            var response = await _httpClient.SendAsync(request);
            string responseContent = await response.Content.ReadAsStringAsync();

            dynamic fbrResponse = JsonConvert.DeserializeObject(responseContent);
            bool isSuccess = response.IsSuccessStatusCode &&
                             ((bool?)fbrResponse.Success ?? (bool?)fbrResponse.success ?? false);

            if (isSuccess)
            {
                invoice.FBRInvoiceNo = fbrResponse.FBRInvoiceNo ?? fbrResponse.fbrInvoiceNo ?? "";
                invoice.Status = "Posted";
                invoice.Remarks = $"Posted to FBR successfully";
            }
            else
            {
                invoice.Status = "FBR_Failed";
                invoice.Remarks = $"FBR Error: {fbrResponse?.Message ?? fbrResponse?.message ?? "Unknown"}";
            }

            invoice.UpdatedDate = DateTime.Now;
            invoice.UpdatedBy = "System";
            await _context.SaveChangesAsync();

            return isSuccess;
        }
    }
}