using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctoDI.Web.Models.DatabaseModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OctoDI.Web.Controllers
{
    public class BuyerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BuyerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==================== LIST BUYERS ====================
        public async Task<IActionResult> Index()
        {
            // Get current subscriptionId from claims
            var subscriptionClaim = User.FindFirst("SubscriptionId")?.Value;
            if (string.IsNullOrEmpty(subscriptionClaim))
                return Unauthorized();

            int currentSubscriptionId = int.Parse(subscriptionClaim);

            var buyers = await _context.Buyers
                .Where(b => b.SubscriptionId == currentSubscriptionId)
                .OrderBy(b => b.BuyerBusinessName)
                .ToListAsync();

            return View(buyers);
        }

        // ==================== CREATE BUYER - POST (JSON Response for AJAX) ====================
        [HttpPost]
        public async Task<IActionResult> Create(
            string BuyerBusinessName,
            string BuyerNTN,
            string BuyerProvince,
            string BuyerAddress,
            string BuyerRegistrationType)
        {
            try
            {
                // Get subscriptionId from logged-in user
                var subscriptionClaim = User.FindFirst("SubscriptionId")?.Value;
                if (string.IsNullOrEmpty(subscriptionClaim))
                    return Unauthorized();

                int subscriptionId = int.Parse(subscriptionClaim);

                // Validation
                if (string.IsNullOrWhiteSpace(BuyerBusinessName))
                    return Json(new { success = false, message = "Business Name is required." });

                if (string.IsNullOrWhiteSpace(BuyerNTN))
                    return Json(new { success = false, message = "NTN is required." });

                if (string.IsNullOrWhiteSpace(BuyerProvince))
                    return Json(new { success = false, message = "Province is required." });

                if (string.IsNullOrWhiteSpace(BuyerAddress))
                    return Json(new { success = false, message = "Address is required." });

                // Check if NTN already exists for this subscription
                var existingBuyer = await _context.Buyers
                    .FirstOrDefaultAsync(b => b.BuyerNTN == BuyerNTN && b.SubscriptionId == subscriptionId);

                if (existingBuyer != null)
                    return Json(new { success = false, message = "Buyer with this NTN already exists." });

                // Create new buyer
                var buyer = new Buyer
                {
                    SubscriptionId = subscriptionId,
                    BuyerBusinessName = BuyerBusinessName,
                    BuyerNTN = BuyerNTN,
                    BuyerProvince = BuyerProvince,
                    BuyerAddress = BuyerAddress,
                    BuyerRegistrationType = BuyerRegistrationType ?? "Unregistered",
                    CreatedDate = DateTime.Now,
                    CreatedBy = User.Identity?.Name ?? "System"
                };

                _context.Buyers.Add(buyer);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Buyer created successfully!",
                    buyerId = buyer.BuyerId,
                    buyerName = buyer.BuyerBusinessName,
                    buyerNTN = buyer.BuyerNTN,
                    buyerProvince = buyer.BuyerProvince
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating buyer: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ==================== GET BUYERS JSON (for dropdown refresh) ====================
        [HttpGet]
        public async Task<IActionResult> GetBuyersJson()
        {
            // Get subscriptionId from logged-in user
            var subscriptionClaim = User.FindFirst("SubscriptionId")?.Value;
            if (string.IsNullOrEmpty(subscriptionClaim))
                return Unauthorized();

            int subscriptionId = int.Parse(subscriptionClaim);

            var buyers = await _context.Buyers
                .Where(b => b.SubscriptionId == subscriptionId)
                .OrderBy(b => b.BuyerBusinessName)
                .Select(b => new
                {
                    id = b.BuyerId,
                    name = b.BuyerBusinessName,
                    ntn = b.BuyerNTN,
                    province = b.BuyerProvince
                })
                .ToListAsync();

            return Json(buyers);
        }

        // ==================== EDIT BUYER - GET ====================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var buyer = await _context.Buyers.FindAsync(id);
            if (buyer == null)
            {
                TempData["Error"] = "Buyer not found!";
                return RedirectToAction("Index");
            }

            // Optional: ensure buyer belongs to current subscription
            var subscriptionClaim = User.FindFirst("SubscriptionId")?.Value;
            if (string.IsNullOrEmpty(subscriptionClaim))
                return Unauthorized();

            int currentSubscriptionId = int.Parse(subscriptionClaim);
            if (buyer.SubscriptionId != currentSubscriptionId)
            {
                TempData["Error"] = "You cannot edit a buyer from another subscription!";
                return RedirectToAction("Index");
            }

            return View(buyer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Buyer model)
        {
            try
            {
                // Add model validation
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var existingBuyer = await _context.Buyers.FindAsync(model.BuyerId);
                if (existingBuyer == null)
                {
                    TempData["Error"] = "Buyer not found!";
                    return RedirectToAction("Index");
                }

                // Ensure buyer belongs to current subscription
                var subscriptionClaim = User.FindFirst("SubscriptionId")?.Value;
                if (string.IsNullOrEmpty(subscriptionClaim))
                    return Unauthorized();

                int currentSubscriptionId = int.Parse(subscriptionClaim);
                if (existingBuyer.SubscriptionId != currentSubscriptionId)
                {
                    TempData["Error"] = "You cannot edit a buyer from another subscription!";
                    return RedirectToAction("Index");
                }

                // Check for duplicate NTN (excluding current buyer)
                var duplicateNtn = await _context.Buyers
                    .AnyAsync(b => b.BuyerNTN == model.BuyerNTN &&
                                  b.BuyerId != model.BuyerId &&
                                  b.SubscriptionId == currentSubscriptionId);

                if (duplicateNtn)
                {
                    ModelState.AddModelError("BuyerNTN", "A buyer with this NTN already exists.");
                    return View(model);
                }

                // Update fields
                existingBuyer.BuyerBusinessName = model.BuyerBusinessName;
                existingBuyer.BuyerNTN = model.BuyerNTN;
                existingBuyer.BuyerProvince = model.BuyerProvince;
                existingBuyer.BuyerAddress = model.BuyerAddress;
                existingBuyer.BuyerRegistrationType = model.BuyerRegistrationType;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Buyer updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                TempData["Error"] = $"Error updating buyer: {ex.Message}";
                return View(model);
            }
        }
        // ==================== DELETE BUYER ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var buyer = await _context.Buyers.FindAsync(id);
                if (buyer == null)
                {
                    TempData["Error"] = "Buyer not found!";
                    return RedirectToAction("Index");
                }

                // Ensure buyer belongs to current subscription
                var subscriptionClaim = User.FindFirst("SubscriptionId")?.Value;
                if (string.IsNullOrEmpty(subscriptionClaim))
                    return Unauthorized();

                int currentSubscriptionId = int.Parse(subscriptionClaim);
                if (buyer.SubscriptionId != currentSubscriptionId)
                {
                    TempData["Error"] = "You cannot delete a buyer from another subscription!";
                    return RedirectToAction("Index");
                }

                // Check if buyer is used in any invoices
                var hasInvoices = await _context.Invoices.AnyAsync(i => i.BuyerId == id);
                if (hasInvoices)
                {
                    TempData["Error"] = "Cannot delete buyer. This buyer is used in existing invoices.";
                    return RedirectToAction("Index");
                }

                _context.Buyers.Remove(buyer);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Buyer deleted successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                TempData["Error"] = $"Unable to delete buyer: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
