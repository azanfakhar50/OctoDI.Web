using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctoDI.Web.Models.DatabaseModels;
using OctoDI.Web.Models.ViewModels;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Newtonsoft.Json;

namespace OctoDI.Web.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ Helper: Check if user is SuperAdmin
        private bool IsSuperAdmin()
        {
            return User.IsInRole("SuperAdmin");
        }

        // ✅ Helper: Get Subscription ID (null for SuperAdmin)
        private int? GetSubscriptionId()
        {
            if (IsSuperAdmin())
                return null;

            var subscriptionIdClaim = User.FindFirst("SubscriptionId")?.Value;
            if (int.TryParse(subscriptionIdClaim, out int subscriptionId))
                return subscriptionId;

            return null;
        }

        // GET: /Product/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var subscriptionId = GetSubscriptionId();

            if (!IsSuperAdmin() && subscriptionId == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            ViewBag.SubscriptionId = subscriptionId;
            ViewBag.IsSuperAdmin = IsSuperAdmin();

            // ✅ Get Units: Global (null) + User's Subscription specific
            ViewBag.Units = await _context.Units
                .Where(u => u.SubscriptionId == null || u.SubscriptionId == subscriptionId)
                .OrderBy(u => u.Name)
                .ToListAsync();

            // ✅ Get Categories: Global (null) + User's Subscription specific
            ViewBag.Categories = await _context.ServiceCategories
                .Where(c => c.SubscriptionId == null || c.SubscriptionId == subscriptionId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View();
        }

        // POST: /Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model)
        {
            var subscriptionId = GetSubscriptionId();

            if (!IsSuperAdmin() && subscriptionId == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            ViewBag.SubscriptionId = subscriptionId;
            ViewBag.IsSuperAdmin = IsSuperAdmin();

            if (ModelState.IsValid)
            {
                var category = await _context.ServiceCategories
                    .FirstOrDefaultAsync(c => c.ServiceCategoryId == model.ServiceCategoryId &&
                                           (c.SubscriptionId == null || c.SubscriptionId == subscriptionId));

                if (category == null)
                {
                    ModelState.AddModelError("ServiceCategoryId", "Invalid category selected.");
                }
                else
                {
                    model.SubscriptionId = IsSuperAdmin() ? null : subscriptionId;
                    model.TaxRate = category.TaxRate;
                    model.CreatedDate = DateTime.Now;
                    model.CreatedBy = User.Identity?.Name ?? "System";
                    model.HSCode = await GenerateUniqueHSCode();

                    _context.Products.Add(model);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = IsSuperAdmin()
                        ? "Global Product added successfully!"
                        : "Product added successfully!";
                    return RedirectToAction("Create");
                }
            }

            ViewBag.Units = await _context.Units
                .Where(u => u.SubscriptionId == null || u.SubscriptionId == subscriptionId)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.Categories = await _context.ServiceCategories
                .Where(c => c.SubscriptionId == null || c.SubscriptionId == subscriptionId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            TempData["Error"] = "Please correct the errors below.";
            return View(model);
        }

        // ✅ Import Excel Page
        [HttpGet]
        public IActionResult ImportProducts()
        {
            var subscriptionId = GetSubscriptionId();

            if (!IsSuperAdmin() && subscriptionId == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            ViewBag.SubscriptionId = subscriptionId;
            ViewBag.IsSuperAdmin = IsSuperAdmin();
            return View();
        }

        // ✅ Preview Excel Data
        [HttpPost]
        public async Task<IActionResult> PreviewImport(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "Please select a valid Excel file.";
                return RedirectToAction("ImportProducts");
            }

            var subscriptionId = GetSubscriptionId();
            var products = new List<Product>();
            var errors = new List<string>();

            try
            {
                using (var stream = new MemoryStream())
                {
                    await excelFile.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var rows = worksheet.RangeUsed().RowsUsed().Skip(1);

                        int rowNumber = 2;
                        foreach (var row in rows)
                        {
                            try
                            {
                                var product = new Product
                                {
                                    ProductDescription = row.Cell(1).GetValue<string>(),
                                    UnitId = row.Cell(2).GetValue<int>(),
                                    ServiceCategoryId = row.Cell(3).GetValue<int>(),
                                    Rate = row.Cell(4).GetValue<decimal>(),
                                    HSCode = row.Cell(5).GetValue<string>(),
                                    TaxRate = row.Cell(6).GetValue<decimal>(),
                                    SubscriptionId = IsSuperAdmin() ? null : subscriptionId,
                                    CreatedDate = DateTime.Now,
                                    CreatedBy = User.Identity?.Name ?? "System"
                                };

                                // ✅ Validate Unit exists (Global or User's)
                                var unitExists = await _context.Units
                                    .AnyAsync(u => u.UnitId == product.UnitId &&
                                             (u.SubscriptionId == null || u.SubscriptionId == subscriptionId));
                                if (!unitExists)
                                {
                                    errors.Add($"Row {rowNumber}: Invalid UnitId {product.UnitId}");
                                }

                                // ✅ Validate Category exists (Global or User's)
                                var categoryExists = await _context.ServiceCategories
                                    .AnyAsync(c => c.ServiceCategoryId == product.ServiceCategoryId &&
                                             (c.SubscriptionId == null || c.SubscriptionId == subscriptionId));
                                if (!categoryExists)
                                {
                                    errors.Add($"Row {rowNumber}: Invalid CategoryId {product.ServiceCategoryId}");
                                }

                                products.Add(product);
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"Row {rowNumber}: {ex.Message}");
                            }

                            rowNumber++;
                        }
                    }
                }

                if (errors.Any())
                {
                    TempData["Errors"] = JsonConvert.SerializeObject(errors);
                    return RedirectToAction("ImportProducts");
                }

                TempData["PreviewProducts"] = JsonConvert.SerializeObject(products);
                return View("ImportPreview", products);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error reading Excel file: {ex.Message}";
                return RedirectToAction("ImportProducts");
            }
        }

        // ✅ Save Imported Products
        [HttpPost]
        public async Task<IActionResult> ConfirmImport()
        {
            var productsJson = TempData["PreviewProducts"]?.ToString();
            if (string.IsNullOrEmpty(productsJson))
            {
                TempData["Error"] = "No products to import. Please upload the file again.";
                return RedirectToAction("ImportProducts");
            }

            var products = JsonConvert.DeserializeObject<List<Product>>(productsJson);
            if (products == null || !products.Any())
            {
                TempData["Error"] = "Invalid product data.";
                return RedirectToAction("ImportProducts");
            }

            var subscriptionId = GetSubscriptionId();

            foreach (var product in products)
            {
                product.SubscriptionId = IsSuperAdmin() ? null : subscriptionId;
                product.CreatedDate = DateTime.Now;
                product.CreatedBy = User.Identity?.Name ?? "System";

                if (string.IsNullOrEmpty(product.HSCode))
                {
                    product.HSCode = await GenerateUniqueHSCode();
                }
            }

            _context.Products.AddRange(products);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{products.Count} products imported successfully!";
            return RedirectToAction("Dashboard");
        }

        // GET: /Product/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var subscriptionId = GetSubscriptionId();

            if (!IsSuperAdmin() && subscriptionId == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            ViewBag.IsSuperAdmin = IsSuperAdmin();

            // ✅ Get Units: Only user's own data
            var units = await _context.Units
                .Where(u => IsSuperAdmin() ? u.SubscriptionId == null : u.SubscriptionId == subscriptionId)
                .OrderBy(u => u.Name)
                .ToListAsync();

            // ✅ Get Categories: Only user's own data
            var categories = await _context.ServiceCategories
                .Where(c => IsSuperAdmin() ? c.SubscriptionId == null : c.SubscriptionId == subscriptionId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // ✅ Get Products: Only user's own data
            var products = await _context.Products
                .Where(p => IsSuperAdmin() ? p.SubscriptionId == null : p.SubscriptionId == subscriptionId)
                .Include(p => p.Unit)
                .Include(p => p.ServiceCategory)
                .OrderBy(p => p.ProductDescription)
                .ToListAsync();

            var vm = new ProductDashboardVM
            {
                Units = units,
                Categories = categories,
                Products = products
            };

            return View(vm);
        }

        // ✅ Generate Unique HS Code
        private async Task<string> GenerateUniqueHSCode()
        {
            string hsCode;
            var random = new Random();
            bool exists;

            do
            {
                hsCode = random.Next(10000000, 99999999).ToString();
                exists = await _context.Products.AnyAsync(p => p.HSCode == hsCode);
            }
            while (exists);

            return hsCode;
        }

        // POST: /Product/CreateUnit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUnit(Unit model)
        {
            var subscriptionId = GetSubscriptionId();

            if (!IsSuperAdmin() && subscriptionId == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError("Name", "Unit name is required.");
            }

            if (!ModelState.IsValid)
            {
                return RedirectToAction("Dashboard");
            }

            model.SubscriptionId = IsSuperAdmin() ? null : subscriptionId;

            _context.Units.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = IsSuperAdmin()
                ? "Global Unit created successfully!"
                : "Unit created successfully!";
            return RedirectToAction("Dashboard");
        }

        // POST: /Product/EditUnit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUnit(Unit model)
        {
            var subscriptionId = GetSubscriptionId();

            if (!IsSuperAdmin() && subscriptionId == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var existingUnit = await _context.Units
                .FirstOrDefaultAsync(u => u.UnitId == model.UnitId &&
                                     (u.SubscriptionId == null || u.SubscriptionId == subscriptionId));

            if (existingUnit == null)
            {
                TempData["Error"] = "Unit not found or access denied.";
                return RedirectToAction("Dashboard");
            }

            existingUnit.Name = model.Name;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Unit updated successfully!";
            return RedirectToAction("Dashboard");
        }

        // POST: /Product/CreateServiceCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateServiceCategory(ServiceCategory model)
        {
            var subscriptionId = GetSubscriptionId();

            if (!IsSuperAdmin() && subscriptionId == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError("Name", "Category name is required.");
            }

            if (model.TaxRate < 0)
            {
                ModelState.AddModelError("TaxRate", "Tax rate must be valid.");
            }

            if (!ModelState.IsValid)
            {
                return RedirectToAction("Dashboard");
            }

            model.SubscriptionId = IsSuperAdmin() ? null : subscriptionId;
            model.CreatedDate = DateTime.Now;
            model.CreatedBy = User.Identity?.Name ?? "System";

            _context.ServiceCategories.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = IsSuperAdmin()
                ? "Global Category created successfully!"
                : "Category created successfully!";
            return RedirectToAction("Dashboard");
        }

        // POST: /Product/EditServiceCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditServiceCategory(ServiceCategory model)
        {
            var subscriptionId = GetSubscriptionId();

            if (!IsSuperAdmin() && subscriptionId == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var existingCategory = await _context.ServiceCategories
                .FirstOrDefaultAsync(c => c.ServiceCategoryId == model.ServiceCategoryId &&
                                     (c.SubscriptionId == null || c.SubscriptionId == subscriptionId));

            if (existingCategory == null)
            {
                TempData["Error"] = "Category not found or access denied.";
                return RedirectToAction("Dashboard");
            }

            existingCategory.Name = model.Name;
            existingCategory.TaxRate = model.TaxRate;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Category updated successfully!";
            return RedirectToAction("Dashboard");
        }

        // GET: /Product/GetTaxRate?categoryId=1
        [HttpGet]
        public async Task<JsonResult> GetTaxRate(int categoryId)
        {
            var subscriptionId = GetSubscriptionId();

            var category = await _context.ServiceCategories
                .FirstOrDefaultAsync(c => c.ServiceCategoryId == categoryId &&
                                     (c.SubscriptionId == null || c.SubscriptionId == subscriptionId));

            if (category == null)
                return Json(new { success = false, taxRate = 0 });

            return Json(new { success = true, taxRate = category.TaxRate });
        }
    }
}