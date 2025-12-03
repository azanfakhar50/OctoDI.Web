using DocumentFormat.OpenXml.Office.CustomUI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OctoDI.Web.Hubs;
using OctoDI.Web.Models.DatabaseModels;
using OctoDI.Web.Models.ViewModels;

namespace OctoDI.Web.Controllers
{
    public class SubscriptionAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IHubContext<SubscriptionHub> _hub;

        public SubscriptionAdminController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher, IHubContext<SubscriptionHub> hub)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _hub = hub;
        }
        [Authorize(Roles = "SubscriptionAdmin")]

        public async Task<IActionResult> BlockSubscription(int subscriptionId)
        {
            var sub = _context.Subscriptions.Find(subscriptionId);
            if (sub != null)
            {
                sub.IsBlocked = true;
                _context.SaveChanges();

                // Notify all users of this subscription
                var users = _context.Users.Where(u => u.SubscriptionId == subscriptionId);
                foreach (var u in users)
                {
                    await _hub.Clients.User(u.UserId.ToString())
                        .SendAsync("ReceiveUpdate", "Your subscription has been blocked by SuperAdmin.");
                }
            }

            return RedirectToAction("Index");
        }


        [Authorize(Roles = "SubscriptionAdmin")]

        // ==================== LIST USERS ====================
        public async Task<IActionResult> Users()
        {
            // 1️⃣ Get subscription ID from logged-in user's claims
            var subscriptionClaim = User.FindFirst("SubscriptionId")?.Value;
            if (string.IsNullOrEmpty(subscriptionClaim))
                return Unauthorized(); // if no subscription claim, deny access

            int currentSubscriptionId = int.Parse(subscriptionClaim);

            // 2️⃣ Fetch users for this subscription
            var users = await _context.Users
                .Where(u => u.SubscriptionId == currentSubscriptionId)
                .OrderByDescending(u => u.CreatedDate)
                .ToListAsync();

            // 3️⃣ Pass subscriptionId to the view if needed
            ViewBag.SubscriptionId = currentSubscriptionId;

            return View(users);
        }

        [Authorize(Roles = "SubscriptionAdmin")]

        // ==================== CREATE USER - GET ====================
        [HttpGet]
        public async Task<IActionResult> CreateUser(int subscriptionId)
        {
            // Get all subscriptions for dropdown
            var subscriptions = await _context.Subscriptions
                .Where(s => s.IsActive)
                .OrderBy(s => s.CompanyName)
                .ToListAsync();

            var model = new User
            {
                SubscriptionId = subscriptionId,
                IsActive = true
            };

            ViewBag.SubscriptionId = subscriptionId;
            ViewBag.Subscriptions = subscriptions;

            return View(model);
        }

        // ==================== CREATE USER - POST ====================
        [Authorize(Roles = "SubscriptionAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(User model)
        {
            try
            {
                // Remove validation for auto-generated/system fields
                ModelState.Remove("UserId");
                ModelState.Remove("CreatedDate");
                ModelState.Remove("CreatedBy");
                ModelState.Remove("UpdatedBy");
                ModelState.Remove("UpdatedDate");
                ModelState.Remove("UserRole");
                ModelState.Remove("IsActive");
                ModelState.Remove("LastLoginDate");
                

                Console.WriteLine("==================== CREATE USER ====================");
                Console.WriteLine($"Username: {model.Username}");
                Console.WriteLine($"Email: {model.Email}");
                Console.WriteLine($"SubscriptionId: {model.SubscriptionId}");

                if (!ModelState.IsValid)
                {
                    Console.WriteLine("\n❌ VALIDATION ERRORS:");
                    foreach (var key in ModelState.Keys)
                    {
                        var state = ModelState[key];
                        if (state.Errors.Count > 0)
                        {
                            Console.WriteLine($"Field: {key}");
                            foreach (var error in state.Errors)
                            {
                                Console.WriteLine($"  - {error.ErrorMessage}");
                            }
                        }
                    }

                    TempData["Error"] = "Please fix validation errors!";
                    ViewBag.SubscriptionId = model.SubscriptionId;
                    ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    return View(model);
                }

                // Check for duplicate username
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == model.Username);

                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Username already exists!");
                    TempData["Error"] = "Username already exists!";
                    ViewBag.SubscriptionId = model.SubscriptionId;
                    ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    return View(model);
                }

                // Check for duplicate email
                var existingEmail = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (existingEmail != null)
                {
                    ModelState.AddModelError("Email", "Email already exists!");
                    TempData["Error"] = "Email already exists!";
                    ViewBag.SubscriptionId = model.SubscriptionId;
                    ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    return View(model);
                }

                // ✅ IMPORTANT: Hash the password
                var tempUser = new User(); // Temporary user for hashing
                model.PasswordHash = _passwordHasher.HashPassword(tempUser, model.PasswordHash);

                // Set system fields
                model.CreatedDate = DateTime.Now;
                model.IsActive = true;
                if (string.IsNullOrEmpty(model.UserRole))
                {
                    model.UserRole = "SubscriptionUser";
                }

                // Get current logged-in user ID
                var currentUserId = int.Parse(User.FindFirst("UserId")?.Value ?? "1");
                model.CreatedBy = currentUserId;
                model.UpdatedBy = currentUserId;

                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ User created successfully! UserId: {model.UserId}");
                TempData["Success"] = $"User '{model.Username}' created successfully!";
                return RedirectToAction("Users", new { subscriptionId = model.SubscriptionId });
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"❌ Database Error: {ex.Message}");
                Console.WriteLine($"❌ Inner Exception: {ex.InnerException?.Message}");

                string errorMessage = "Unable to create user. ";
                if (ex.InnerException?.Message.Contains("UNIQUE") == true)
                {
                    errorMessage += "Username or Email already exists!";
                }
                else
                {
                    errorMessage += ex.InnerException?.Message ?? ex.Message;
                }

                TempData["Error"] = errorMessage;
                ViewBag.SubscriptionId = model.SubscriptionId;
                ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Unexpected Error: {ex.Message}");
                TempData["Error"] = $"Error: {ex.Message}";
                ViewBag.SubscriptionId = model.SubscriptionId;
                ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                return View(model);
            }
        }
        [Authorize(Roles = "SubscriptionAdmin")]
        public async Task<IActionResult> Index()
        {
            try
            {
                // 🔹 Get current logged-in Subscription ID from Claims
                var subscriptionId = int.Parse(User.FindFirst("SubscriptionId")?.Value ?? "0");

                if (subscriptionId == 0)
                {
                    TempData["Error"] = "Subscription context not found!";
                    return RedirectToAction("Login", "Account");
                }

                // 🔹 Get subscription details
                var subscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

                if (subscription == null)
                {
                    TempData["Error"] = "Subscription not found!";
                    return RedirectToAction("Login", "Account");
                }

                // 🔹 Count all data only related to this subscription
                var totalUsers = await _context.Users.CountAsync(u => u.SubscriptionId == subscriptionId);
                var activeUsers = await _context.Users.CountAsync(u => u.SubscriptionId == subscriptionId && u.IsActive);
                var totalInvoices = await _context.Invoices.CountAsync(i => i.SubscriptionId == subscriptionId);
                var pendingInvoices = await _context.Invoices.CountAsync(i => i.SubscriptionId == subscriptionId && i.Status == "Pending");
                var totalBuyers = await _context.Buyers.CountAsync(b => b.SubscriptionId == subscriptionId);
                //var activeBuyers = await _context.Buyers.CountAsync(b => b.SubscriptionId == subscriptionId && b.IsActive);

                // 🔹 Prepare dashboard ViewModel
                var model = new SubscriptionAdminDashboardViewModel
                {
                    SubscriptionId = subscription.SubscriptionId,
                    SubscriptionName = subscription.CompanyName,
                    CreatedDate = subscription.CreatedDate,
                    IsActive = subscription.IsActive,
                    SubscriptionEndDate = subscription.SubscriptionEndDate,
                    isActive = !string.IsNullOrEmpty(subscription.FbrSecurityToken),
                    TokenExpiryDate = subscription.TokenExpiryDate,
                    LastFbrSync = subscription.SubscriptionEndDate,
                    TotalUsers = totalUsers,
                    ActiveUsers = activeUsers,
                    TotalInvoices = totalInvoices,
                    PendingInvoices = pendingInvoices,
                    TotalBuyers = totalBuyers,
                   // ActiveBuyers = activeBuyers
                };

                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in Index(): {ex.Message}");
                TempData["Error"] = $"Error loading dashboard: {ex.Message}";
                return RedirectToAction("Login", "Account");
            }
        }


        // ==================== EDIT USER - GET ====================
        [Authorize(Roles = "SubscriptionAdmin")]

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found!";
                return RedirectToAction("Users", new { subscriptionId = ViewBag.SubscriptionId });
            }

            var subscriptions = await _context.Subscriptions
                .Where(s => s.IsActive)
                .OrderBy(s => s.CompanyName)
                .ToListAsync();

            ViewBag.SubscriptionId = user.SubscriptionId;
            ViewBag.Subscriptions = new SelectList(subscriptions, "SubscriptionId", "CompanyName", user.SubscriptionId);
            return View(user);
        }

        // ==================== EDIT USER - POST ====================
        [Authorize(Roles = "SubscriptionAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User model, string newPassword = null)
        {
            try
            {
                ModelState.Remove("PasswordHash"); // Password is optional on edit
                ModelState.Remove("Subscription");

                if (!ModelState.IsValid)
                {
                    ViewBag.SubscriptionId = model.SubscriptionId;
                    var subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    ViewBag.Subscriptions = new SelectList(subscriptions, "SubscriptionId", "CompanyName", model.SubscriptionId);
                    return View(model);
                }

                var existingUser = await _context.Users.FindAsync(model.UserId);
                if (existingUser == null)
                {
                    TempData["Error"] = "User not found!";
                    return RedirectToAction("Users", new { subscriptionId = model.SubscriptionId });
                }

                // Check for duplicate username (excluding current user)
                var duplicateUsername = await _context.Users
                    .AnyAsync(u => u.Username == model.Username && u.UserId != model.UserId);
                if (duplicateUsername)
                {
                    ModelState.AddModelError("Username", "Username already exists!");
                    ViewBag.SubscriptionId = model.SubscriptionId;
                    var subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    ViewBag.Subscriptions = new SelectList(subscriptions, "SubscriptionId", "CompanyName", model.SubscriptionId);
                    return View(model);
                }

                // Check for duplicate email (excluding current user)
                var duplicateEmail = await _context.Users
                    .AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId);
                if (duplicateEmail)
                {
                    ModelState.AddModelError("Email", "Email already exists!");
                    ViewBag.SubscriptionId = model.SubscriptionId;
                    var subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    ViewBag.Subscriptions = new SelectList(subscriptions, "SubscriptionId", "CompanyName", model.SubscriptionId);
                    return View(model);
                }

                // Update fields
                existingUser.Username = model.Username;
                existingUser.Email = model.Email;
                existingUser.FirstName = model.FirstName;
                existingUser.LastName = model.LastName;
                existingUser.PhoneNumber = model.PhoneNumber;
                existingUser.UserRole = model.UserRole;
                existingUser.IsActive = model.IsActive;
                existingUser.Remarks = model.Remarks;
                existingUser.SubscriptionId = model.SubscriptionId;
                existingUser.IsTwoFactorEnabled = model.IsTwoFactorEnabled;
                existingUser.UpdatedDate = DateTime.Now;
                existingUser.UpdatedBy = int.Parse(User.FindFirst("UserId")?.Value ?? "1");

                // Update password only if provided
                if (!string.IsNullOrEmpty(newPassword))
                {
                    existingUser.PasswordHash = _passwordHasher.HashPassword(existingUser, newPassword);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "User updated successfully!";
                return RedirectToAction("Users", new { subscriptionId = existingUser.SubscriptionId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                TempData["Error"] = $"Error: {ex.Message}";
                ViewBag.SubscriptionId = model.SubscriptionId;
                var subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                ViewBag.Subscriptions = new SelectList(subscriptions, "SubscriptionId", "CompanyName", model.SubscriptionId);
                return View(model);
            }
        }

        // ==================== DELETE USER - GET (Confirmation Page) ====================
        [HttpGet]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found!";
                return RedirectToAction("Users", new { subscriptionId = ViewBag.SubscriptionId });
            }

            // Don't allow deleting yourself
            var currentUserId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
            if (user.UserId == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own account!";
                return RedirectToAction("Users", new { subscriptionId = user.SubscriptionId });
            }

            ViewBag.SubscriptionId = user.SubscriptionId;
            return View(user);
        }

        // ==================== DELETE USER - POST (Actual Deletion) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id, int subscriptionId)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    TempData["Error"] = "User not found!";
                    return RedirectToAction("Users", new { subscriptionId });
                }

                // Don't allow deleting yourself
                var currentUserId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
                if (user.UserId == currentUserId)
                {
                    TempData["Error"] = "You cannot delete your own account!";
                    return RedirectToAction("Users", new { subscriptionId });
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"User '{user.Username}' deleted successfully!";
                return RedirectToAction("Users", new { subscriptionId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                TempData["Error"] = $"Unable to delete user: {ex.Message}";
                return RedirectToAction("Users", new { subscriptionId });
            }
        }

        [Authorize(Roles = "SubscriptionUser")]
        public async Task<IActionResult> UserDashboard()
        {
            try
            {
                // Get current logged-in user ID
                var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");

                // Get subscription ID from claims
                var subscriptionId = int.Parse(User.FindFirst("SubscriptionId")?.Value ?? "0");

                if (subscriptionId == 0)
                {
                    TempData["Error"] = "Subscription not found!";
                    return RedirectToAction("Login", "Account");
                }

                // User-based stats
                var totalInvoices = await _context.Invoices
                    .CountAsync(i => i.SubscriptionId == subscriptionId );

                var totalBuyers = await _context.Buyers
                    .CountAsync(b => b.SubscriptionId == subscriptionId);

                var totalProducts = await _context.Products
                    .CountAsync(p => p.SubscriptionId == subscriptionId);

                var model = new SubscriptionUserDashboardViewModel
                {
                    SubscriptionId = subscriptionId,
                    UserId = userId,
                    TotalInvoices = totalInvoices,
                    TotalBuyers = totalBuyers,
                    TotalProducts = totalProducts
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading dashboard: {ex.Message}";
                return RedirectToAction("Login", "Account");
            }
        }
        [Authorize(Roles = "SubscriptionAdmin")]

        private async Task<(bool isExpired, bool tokenExpired, string message)> CheckSubscriptionStatus(int subscriptionId)
        {
            var subscription = await _context.Subscriptions.FindAsync(subscriptionId);
            if (subscription == null)
                return (true, true, "⚠ Subscription not found!");

            bool isExpired = subscription.SubscriptionEndDate.HasValue && subscription.SubscriptionEndDate.Value < DateTime.Now;
            bool tokenExpired = subscription.FbrSecurityToken == null ||
                                (subscription.TokenExpiryDate.HasValue && subscription.TokenExpiryDate.Value < DateTime.Now);

            string message = "";

            if (isExpired && tokenExpired)
                message = $"⚠ Your subscription and token for '{subscription.CompanyName}' have expired!";
            else if (isExpired)
                message = $"⚠ Your subscription for '{subscription.CompanyName}' has expired on {subscription.SubscriptionEndDate:dd MMM yyyy}!";
            else if (tokenExpired)
                message = $"⚠ Your subscription token for '{subscription.CompanyName}' has expired!";

            return (isExpired, tokenExpired, message);
        }

        

    }
}