using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctoDI.Web.Models.DatabaseModels;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OctoDI.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class GlobalSaleRateController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GlobalSaleRateController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /GlobalSaleRate
        public async Task<IActionResult> Index()
        {
            // Show only products with SubscriptionId = null (global rates)
            var rates = await _context.Products
                .Include(p => p.Unit)
                .Include(p => p.ServiceCategory)
                .Where(p => p.SubscriptionId == null)
                .OrderBy(p => p.ProductDescription)
                .ToListAsync();
            return View(rates);
        }

        // GET: /GlobalSaleRate/Create
        public IActionResult Create()
        {
            // Get all units and categories (regardless of subscription)
            ViewBag.Units = _context.Units
                .Select(u => new { u.UnitId, u.Name })
                .Distinct()
                .OrderBy(u => u.Name)
                .ToList();

            ViewBag.ServiceCategories = _context.ServiceCategories
                .Select(s => new { s.ServiceCategoryId, s.Name })
                .Distinct()
                .OrderBy(s => s.Name)
                .ToList();

            return View();
        }

        // POST: /GlobalSaleRate/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model)
        {
            // Remove SubscriptionId validation error
            ModelState.Remove("SubscriptionId");
            ModelState.Remove("Subscription");

            if (ModelState.IsValid)
            {
                // Set created info
                model.CreatedDate = DateTime.Now;
                model.CreatedBy = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "SuperAdmin";

                // Set SubscriptionId to null for global rates
                model.SubscriptionId = null;

                // Save directly to Product table
                _context.Products.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Global sale rate added successfully!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Units = _context.Units.Select(u => new { u.UnitId, u.Name }).Distinct().OrderBy(u => u.Name).ToList();
            ViewBag.ServiceCategories = _context.ServiceCategories.Select(s => new { s.ServiceCategoryId, s.Name }).Distinct().OrderBy(s => s.Name).ToList();
            TempData["Error"] = "Please correct the errors below.";
            return View(model);
        }

        // GET: /GlobalSaleRate/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var rate = await _context.Products.FindAsync(id);
            if (rate == null)
                return NotFound();

            ViewBag.Units = _context.Units.Select(u => new { u.UnitId, u.Name }).Distinct().OrderBy(u => u.Name).ToList();
            ViewBag.ServiceCategories = _context.ServiceCategories.Select(s => new { s.ServiceCategoryId, s.Name }).Distinct().OrderBy(s => s.Name).ToList();
            return View(rate);
        }

        // POST: /GlobalSaleRate/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model)
        {
            if (id != model.ProductId)
                return BadRequest();

            ModelState.Remove("SubscriptionId");
            ModelState.Remove("Subscription");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Products.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Global sale rate updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    TempData["Error"] = $"Error updating global sale rate: {ex.Message}";
                }
            }

            ViewBag.Units = _context.Units.Select(u => new { u.UnitId, u.Name }).Distinct().OrderBy(u => u.Name).ToList();
            ViewBag.ServiceCategories = _context.ServiceCategories.Select(s => new { s.ServiceCategoryId, s.Name }).Distinct().OrderBy(s => s.Name).ToList();
            return View(model);
        }

        // GET: /GlobalSaleRate/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var rate = await _context.Products.FindAsync(id);
            if (rate == null)
                return NotFound();

            _context.Products.Remove(rate);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Global sale rate deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /GlobalSaleRate/Import
        public IActionResult Import()
        {
            return View();
        }

        // POST: /GlobalSaleRate/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid Excel file.";
                return View();
            }

            if (!file.FileName.EndsWith(".xlsx") && !file.FileName.EndsWith(".xls"))
            {
                TempData["Error"] = "Only Excel files (.xlsx, .xls) are allowed.";
                return View();
            }

            try
            {
                using (var stream = file.OpenReadStream())
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header row

                    int successCount = 0;
                    int errorCount = 0;
                    var errors = new System.Collections.Generic.List<string>();

                    int rowNumber = 2; // Start from row 2 (after header)
                    foreach (var row in rows)
                    {
                        try
                        {
                            var productDescription = row.Cell(1).GetString();
                            var hsCode = row.Cell(2).GetString();
                            var unitName = row.Cell(3).GetString();
                            var categoryName = row.Cell(4).GetString();
                            var rateText = row.Cell(5).GetString();
                            var taxRateText = row.Cell(6).GetString();

                            // Validation
                            if (string.IsNullOrWhiteSpace(productDescription))
                            {
                                errors.Add($"Row {rowNumber}: Product Description is required.");
                                errorCount++;
                                rowNumber++;
                                continue;
                            }

                            // Find Unit
                            var unit = await _context.Units.FirstOrDefaultAsync(u => u.Name == unitName);
                            if (unit == null)
                            {
                                errors.Add($"Row {rowNumber}: Unit '{unitName}' not found.");
                                errorCount++;
                                rowNumber++;
                                continue;
                            }

                            // Find Category
                            var category = await _context.ServiceCategories.FirstOrDefaultAsync(c => c.Name == categoryName);
                            if (category == null)
                            {
                                errors.Add($"Row {rowNumber}: Category '{categoryName}' not found.");
                                errorCount++;
                                rowNumber++;
                                continue;
                            }

                            // Parse Rate
                            if (!decimal.TryParse(rateText, out decimal rate))
                            {
                                errors.Add($"Row {rowNumber}: Invalid Rate value.");
                                errorCount++;
                                rowNumber++;
                                continue;
                            }

                            // Parse Tax Rate
                            if (!decimal.TryParse(taxRateText, out decimal taxRate))
                            {
                                errors.Add($"Row {rowNumber}: Invalid Tax Rate value.");
                                errorCount++;
                                rowNumber++;
                                continue;
                            }

                            // Create Product
                            var product = new Product
                            {
                                ProductDescription = productDescription,
                                HSCode = hsCode,
                                UnitId = unit.UnitId,
                                ServiceCategoryId = category.ServiceCategoryId,
                                Rate = rate,
                                TaxRate = taxRate,
                                SubscriptionId = null,
                                CreatedDate = DateTime.Now,
                                CreatedBy = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "SuperAdmin"
                            };

                            _context.Products.Add(product);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Row {rowNumber}: {ex.Message}");
                            errorCount++;
                        }

                        rowNumber++;
                    }

                    await _context.SaveChangesAsync();

                    if (successCount > 0)
                    {
                        TempData["Success"] = $"Successfully imported {successCount} records.";
                    }

                    if (errorCount > 0)
                    {
                        TempData["Error"] = $"{errorCount} records failed. Errors: " + string.Join("; ", errors.Take(5));
                    }

                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error importing file: {ex.Message}";
                return View();
            }
        }

        // GET: /GlobalSaleRate/DownloadTemplate
        public IActionResult DownloadTemplate()
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("GlobalSaleRates");

                // Headers
                worksheet.Cell(1, 1).Value = "Product Description";
                worksheet.Cell(1, 2).Value = "HS Code";
                worksheet.Cell(1, 3).Value = "Unit Name";
                worksheet.Cell(1, 4).Value = "Category Name";
                worksheet.Cell(1, 5).Value = "Rate";
                worksheet.Cell(1, 6).Value = "Tax Rate";

                // Style headers
                var headerRange = worksheet.Range(1, 1, 1, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Sample data
                worksheet.Cell(2, 1).Value = "Sample Product";
                worksheet.Cell(2, 2).Value = "1234.56.78";
                worksheet.Cell(2, 3).Value = "Kg";
                worksheet.Cell(2, 4).Value = "General";
                worksheet.Cell(2, 5).Value = 100.50;
                worksheet.Cell(2, 6).Value = 18.00;

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "GlobalSaleRates_Template.xlsx");
            }
        }
    }
}