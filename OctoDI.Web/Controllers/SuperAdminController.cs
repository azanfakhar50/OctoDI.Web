using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OctoDI.Web.Models.DatabaseModels;
using OctoDI.Web.Models.ViewModels;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OctoDI.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminController : Controller
    {
        private static Random _random = new Random();
        private readonly ApplicationDbContext _context;
        private readonly string? _jwtSecretKey;
        private readonly string? _jwtIssuer;
        private readonly string? _jwtAudience;
        private readonly int _jwtExpiryDays;
        private readonly IPasswordHasher<User> _passwordHasher;


        public SuperAdminController(ApplicationDbContext context, IConfiguration config, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _jwtSecretKey = config.GetValue<string>("JwtSettings:SecretKey");
            _jwtIssuer = config.GetValue<string>("JwtSettings:Issuer");
            _jwtAudience = config.GetValue<string>("JwtSettings:Audience");
            _jwtExpiryDays = config.GetValue<int>("JwtSettings:ExpiryDays", 30);
            _passwordHasher = passwordHasher;
        }
        public async Task<IActionResult> Dashboard(int page = 1, int pageSize = 10)
{
    // Calculate total subscriptions for pagination
    var totalSubscriptions = await _context.Subscriptions.CountAsync();
    var totalPages = (int)Math.Ceiling(totalSubscriptions / (double)pageSize);
    
    // Set ViewBag for pagination
    ViewBag.PageNumber = page;
    ViewBag.TotalPages = totalPages;
    ViewBag.PageSize = pageSize;
    
    var model = new SuperAdminDashboardViewModel
    {
        TotalSubscriptions = totalSubscriptions,
        ActiveSubscriptions = await _context.Subscriptions.CountAsync(s => s.IsActive && !s.IsBlocked),
        BlockedSubscriptions = await _context.Subscriptions.CountAsync(s => s.IsBlocked),
        TotalUsers = await _context.Users.CountAsync(u => u.UserRole == "SubscriptionAdmin"),
        ActiveUsers = await _context.Users.CountAsync(u => u.IsActive && u.UserRole == "SubscriptionAdmin"),
        TotalInvoices = await _context.Invoices.CountAsync(),
        PendingInvoices = await _context.Invoices.CountAsync(i => i.Status == "Pending"),
        
        // Paginated Recent Subscriptions
        RecentSubscriptions = await _context.Subscriptions
            .OrderByDescending(s => s.CreatedDate)
            .Skip((page - 1) * pageSize)  // Skip previous pages
            .Take(pageSize)               // Take only current page items
            .Select(s => new RecentSubscriptionViewModel
            {
                SubscriptionId = s.SubscriptionId,
                CompanyName = s.CompanyName,
                IsActive = s.IsActive,
                IsBlocked = s.IsBlocked,
                TotalUsers = s.Users.Count(),
                ActiveUsers = s.Users.Count(u => u.IsActive),
                TotalInvoices = _context.Invoices.Count(i => i.SubscriptionId == s.SubscriptionId),
                FbrConfigured = !string.IsNullOrEmpty(s.FbrSecurityToken)
            }).ToListAsync()
    };
    
    return View(model);
}
        


        // View All Subscriptions with Pagination and Filtering
        public async Task<IActionResult> Index(
            int page = 1, 
            int pageSize = 10,
            string? fbrId = null,
            string? companyName = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? status = null)
        {
            try
            {
                // Start with base query
                var query = _context.Subscriptions
                    .Include(s => s.Users)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(fbrId))
                {
                    query = query.Where(s => s.FbrSubscriptionId != null && 
                                           s.FbrSubscriptionId.Contains(fbrId));
                }

                if (!string.IsNullOrWhiteSpace(companyName))
                {
                    query = query.Where(s => s.CompanyName != null && 
                                           s.CompanyName.Contains(companyName));
                }

                if (startDate.HasValue)
                {
                    query = query.Where(s => s.SubscriptionStartDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(s => s.SubscriptionEndDate <= endDate.Value);
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    switch (status.ToLower())
                    {
                        case "active":
                            query = query.Where(s => s.IsActive && !s.IsBlocked);
                            break;
                        case "inactive":
                            query = query.Where(s => !s.IsActive && !s.IsBlocked);
                            break;
                        case "blocked":
                            query = query.Where(s => s.IsBlocked);
                            break;
                    }
                }

                // Get total count for pagination
                var totalRecords = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                // Ensure page is within valid range
                page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

                // Get paginated data
                var subscriptions = await query
                    .OrderByDescending(s => s.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Pass pagination info to view
                ViewBag.PageNumber = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalRecords = totalRecords;

                return View(subscriptions);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load subscriptions: " + ex.Message;
                return View(new List<Subscription>());
            }
        }

        private string GenerateJwtToken(Subscription subscription)
        {
            try
            {
                if (string.IsNullOrEmpty(_jwtSecretKey))
                    throw new InvalidOperationException("JWT Secret Key is not configured in appsettings.json");

                // Ensure the key is at least 256 bits (32 characters)
                var secretKey = _jwtSecretKey;
                if (secretKey.Length < 32)
                {
                    // Pad the key to make it at least 32 characters
                    secretKey = secretKey.PadRight(32, '0');
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(secretKey);

                // Double check key size
                if (key.Length < 32)
                {
                    throw new InvalidOperationException($"JWT Secret Key must be at least 32 characters. Current length: {_jwtSecretKey.Length}");
                }

                var claims = new List<Claim>
                {
                    new Claim("CompanyName", subscription.CompanyName ?? "")
                };

                if (subscription.SubscriptionId > 0)
                    claims.Add(new Claim("SubscriptionId", subscription.SubscriptionId.ToString()));

                var expiryDate = subscription.SubscriptionEndDate ?? DateTime.UtcNow.AddDays(_jwtExpiryDays);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = expiryDate,
                    Issuer = _jwtIssuer,
                    Audience = _jwtAudience,
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                return tokenHandler.WriteToken(token);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to generate JWT token: " + ex.Message);
            }
        }

        // Generate unique FBR Subscription ID
        public static string GenerateFbrSubscriptionId()
        {
            var random = new Random();
            string datePart = DateTime.Now.ToString("yyMMdd"); // YYMMDD format
            string randomPart = random.Next(1000, 9999).ToString(); // 4-digit random
            return $"FBR-{datePart}-{randomPart}"; // e.g., FBR-241107-5432
        }

        [HttpGet]
        public IActionResult CreateSubscription()
        {
            var model = new Subscription
            {
                IsActive = true,
                IsBlocked = false,
                SubscriptionStartDate = DateTime.Now,
                SubscriptionEndDate = DateTime.Now.AddMonths(1),
                IsTwoFactorEnabled = false // default
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubscription(Subscription model)
        {
            // Remove auto-generated fields to prevent validation errors
            ModelState.Remove("FbrSecurityToken");
            ModelState.Remove("TokenExpiryDate");
            ModelState.Remove("CreatedDate");
            ModelState.Remove("Users");
            ModelState.Remove("SubscriptionSetting");
            ModelState.Remove("Buyers");
            ModelState.Remove("IsTwoFactorEnabled");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill all required fields correctly!";
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.CompanyName))
            {
                TempData["Error"] = "Company Name is required!";
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.NtnCnic))
            {
                TempData["Error"] = "NTN/CNIC is required!";
                return View(model);
            }

            if (!model.SubscriptionStartDate.HasValue || !model.SubscriptionEndDate.HasValue)
            {
                TempData["Error"] = "Subscription dates are required!";
                return View(model);
            }

            if (model.SubscriptionEndDate <= model.SubscriptionStartDate)
            {
                TempData["Error"] = "End Date must be after Start Date!";
                return View(model);
            }

            if (!model.MaxUsers.HasValue || model.MaxUsers.Value < 1)
            {
                TempData["Error"] = "Max Users must be at least 1!";
                return View(model);
            }

            var existingSubscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.NtnCnic == model.NtnCnic);

            if (existingSubscription != null)
            {
                TempData["Error"] = "A subscription with this NTN/CNIC already exists!";
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                model.CreatedDate = DateTime.Now;
                model.IsActive = true;
                model.IsBlocked = false;
                model.CreatedBy = GetCurrentUserId();
                model.FbrSubscriptionId = GenerateFbrSubscriptionId();

                // ✅ STEP 1: Save subscription first to get SubscriptionId
                _context.Subscriptions.Add(model);
                await _context.SaveChangesAsync();

                // ✅ STEP 2: Generate JWT Token
                var jwtToken = GenerateJwtToken(model);
                model.FbrSecurityToken = jwtToken;
                model.TokenExpiryDate = model.SubscriptionEndDate ?? DateTime.UtcNow.AddDays(_jwtExpiryDays);
                model.IsTwoFactorEnabled = model.IsTwoFactorEnabled;

                _context.Subscriptions.Update(model);
                await _context.SaveChangesAsync();

                // ✅ STEP 3: Create SubscriptionSettings with SAME JWT token
                var setting = new SubscriptionSetting
                {
                    SubscriptionId = model.SubscriptionId,
                    FbrBaseUrl = "https://localhost:7118/api/fbrmock",
                    // ✅ CRITICAL: Use the SAME JWT token, NOT a new GUID
                    FbrToken = jwtToken,
                    SellerNTN = model.NtnCnic,
                    SellerBusinessName = model.BusinessName ?? model.CompanyName,
                    SellerAddress = model.Address,
                    SellerProvince = model.Province,
                    CreatedBy = User.Identity?.Name ?? "System",
                    CreatedDate = DateTime.Now,
                    IsActive = true
                };

                _context.SubscriptionSettings.Add(setting);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                Console.WriteLine($"✅ Subscription {model.SubscriptionId} created successfully");
                Console.WriteLine($"   JWT Token generated and saved in both places");
                Console.WriteLine($"   Token (first 30 chars): {jwtToken?.Substring(0, Math.Min(30, jwtToken.Length))}...");

                TempData["Success"] = "Subscription created successfully!";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error creating subscription: {ex.Message}");
                TempData["Error"] = "Failed to create subscription: " + ex.Message;
                return View(model);
            }
        }

        //// GET: /SuperAdmin/Settings/{subscriptionId}
        //[HttpGet("SuperAdmin/Settings/{subscriptionId}")]
        //public async Task<IActionResult> Settings(int subscriptionId)
        //{
        //    if (subscriptionId == 0)
        //    {
        //        TempData["Error"] = "Subscription context not found!";
        //        return RedirectToAction("Index");
        //    }

        //    // Fetch subscription settings and map to ViewModel
        //    var dbSettings = await _context.SubscriptionSettings
        //        .Include(s => s.Subscription) // optional: if you want SubscriptionName
        //        .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

        //    var model = new SubscriptionFbrSettingsViewModel
        //    {
        //        SubscriptionId = subscriptionId,
        //        SubscriptionName = dbSettings?.Subscription?.CompanyName ?? "",
        //        FbrBaseUrl = dbSettings?.FbrBaseUrl ?? "",
        //        FbrToken = dbSettings?.FbrToken ?? "",
        //        SellerNTN = dbSettings?.SellerNTN ?? "",
        //        SellerBusinessName = dbSettings?.SellerBusinessName ?? "",
        //        SellerAddress = dbSettings?.SellerAddress ?? "",
        //        SellerProvince = dbSettings?.SellerProvince ?? ""
        //    };

        //    return View(model);
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> SaveSettings(SubscriptionFbrSettingsViewModel model)
        //{
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //        {
        //            TempData["Error"] = "Please fix validation errors!";
        //            return View("Settings", model);
        //        }

        //        var existing = await _context.SubscriptionSettings
        //            .FirstOrDefaultAsync(s => s.SubscriptionId == model.SubscriptionId);

        //        var currentUserId = User.FindFirst("UserId")?.Value ?? "1";

        //        if (existing != null)
        //        {
        //            // ✅ Update existing - Now includes Address and Province
        //            existing.FbrBaseUrl = model.FbrBaseUrl?.TrimEnd('/'); // Remove trailing slash
        //            existing.FbrToken = model.FbrToken;
        //            existing.SellerNTN = model.SellerNTN;
        //            existing.SellerBusinessName = model.SellerBusinessName;
        //            existing.SellerAddress = model.SellerAddress;  // ✅ FIXED: Now saved
        //            existing.SellerProvince = model.SellerProvince; // ✅ FIXED: Now saved
        //            existing.UpdatedDate = DateTime.Now;
        //            existing.UpdatedBy = currentUserId;
        //            existing.IsActive = true;

        //            _context.SubscriptionSettings.Update(existing);
        //            Console.WriteLine($"✅ Updated settings for subscription {model.SubscriptionId}");
        //        }
        //        else
        //        {
        //            // ✅ Create new - Now includes Address and Province
        //            var newSetting = new SubscriptionSetting
        //            {
        //                SubscriptionId = model.SubscriptionId,
        //                FbrBaseUrl = model.FbrBaseUrl?.TrimEnd('/'), // Remove trailing slash
        //                FbrToken = model.FbrToken,
        //                SellerNTN = model.SellerNTN,
        //                SellerBusinessName = model.SellerBusinessName,
        //                SellerAddress = model.SellerAddress,      // ✅ FIXED: Now saved
        //                SellerProvince = model.SellerProvince,    // ✅ FIXED: Now saved
        //                CreatedDate = DateTime.Now,
        //                CreatedBy = currentUserId,
        //                UpdatedBy = currentUserId,
        //                IsActive = true
        //            };
        //            _context.SubscriptionSettings.Add(newSetting);
        //            Console.WriteLine($"✅ Created new settings for subscription {model.SubscriptionId}");
        //        }

        //        await _context.SaveChangesAsync();
        //        TempData["Success"] = "Settings saved successfully!";
        //        return RedirectToAction("Settings", new { subscriptionId = model.SubscriptionId });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ Error saving settings: {ex.Message}");
        //        TempData["Error"] = $"Error: {ex.Message}";
        //        return View("Settings", model);
        //    }
        //}


        [HttpGet]
        public async Task<IActionResult> CreateUser(int? subscriptionId)
        {
            var subscriptions = await _context.Subscriptions
                .Where(s => s.IsActive)
                .OrderBy(s => s.CompanyName)
                .ToListAsync();

            ViewBag.Subscriptions = subscriptions;

            var user = new User
            {
                IsActive = true,
                SubscriptionId = subscriptionId ?? 0
            };

            return View(user);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(User model)
        {
            try
            {
                // Ignore system fields for model validation
                ModelState.Remove("UserId");
                ModelState.Remove("CreatedDate");
                ModelState.Remove("CreatedBy");
                ModelState.Remove("UpdatedBy");
                ModelState.Remove("UpdatedDate");
                ModelState.Remove("LastLoginDate");

                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Please fix validation errors!";
                    ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    return View(model);
                }

                // ✅ Validate Subscription selection
                if (model.SubscriptionId == 0)
                {
                    TempData["Error"] = "Please select a subscription!";
                    ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    return View(model);
                }

                // ✅ Check duplicates
                if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                {
                    TempData["Error"] = "Username already exists!";
                    ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    return View(model);
                }

                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    TempData["Error"] = "Email already exists!";
                    ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                    return View(model);
                }

                // ✅ Hash password (ensure _passwordHasher is injected in constructor)
                model.PasswordHash = _passwordHasher.HashPassword(new User(), model.PasswordHash);

                // ✅ Set audit fields
                var currentUserId = int.Parse(User.FindFirst("UserId")?.Value ?? "1");
                model.CreatedBy = currentUserId;
                model.UpdatedBy = currentUserId;
                model.CreatedDate = DateTime.Now;
                model.IsActive = true;

                // ✅ Default role fallback
                if (string.IsNullOrEmpty(model.UserRole))
                    model.UserRole = "SubscriptionUser";

                // ✅ Save to DB
                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"User '{model.Username}' created successfully!";
                return RedirectToAction("CreateUser", new { subscriptionId = model.SubscriptionId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                ViewBag.Subscriptions = await _context.Subscriptions.Where(s => s.IsActive).ToListAsync();
                return View(model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var subscription = await _context.Subscriptions.FindAsync(id);
                if (subscription == null)
                {
                    TempData["Error"] = "Subscription not found!";
                    return RedirectToAction(nameof(Index));
                }
                return View(subscription);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading subscription: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Subscription model)
        {
            if (id != model.SubscriptionId)
            {
                TempData["Error"] = "Invalid subscription ID!";
                return RedirectToAction(nameof(Index));
            }

            // Remove validation for auto-generated fields
            ModelState.Remove("FbrSecurityToken");
            ModelState.Remove("TokenExpiryDate");
            ModelState.Remove("Users");
            ModelState.Remove("SubscriptionSetting");
            ModelState.Remove("Buyers");
            ModelState.Remove("CreatedDate");
            ModelState.Remove("CreatedBy");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill all required fields correctly!";
                return View(model);
            }

            // Validate dates
            if (model.SubscriptionStartDate.HasValue && model.SubscriptionEndDate.HasValue)
            {
                if (model.SubscriptionEndDate <= model.SubscriptionStartDate)
                {
                    TempData["Error"] = "End Date must be after Start Date!";
                    return View(model);
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existing = await _context.Subscriptions
                    .Include(s => s.SubscriptionSetting)
                    .FirstOrDefaultAsync(s => s.SubscriptionId == id);

                if (existing == null)
                {
                    TempData["Error"] = "Subscription not found!";
                    return RedirectToAction(nameof(Index));
                }

                // Check for duplicate NTN/CNIC (ignoring case)
                var duplicateNtn = await _context.Subscriptions
                    .AnyAsync(s => s.NtnCnic.ToLower() == model.NtnCnic.ToLower() && s.SubscriptionId != id);

                if (duplicateNtn)
                {
                    TempData["Error"] = "Another subscription with this NTN/CNIC already exists!";
                    return View(model);
                }

                // ✅ Store old values to check what changed
                bool addressChanged = existing.Address != model.Address;
                bool provinceChanged = existing.Province != model.Province;
                bool businessNameChanged = existing.BusinessName != model.BusinessName;
                bool ntnChanged = existing.NtnCnic != model.NtnCnic;

                // Update basic fields
                existing.CompanyName = model.CompanyName;
                existing.BusinessName = model.BusinessName;
                existing.NtnCnic = model.NtnCnic;
                existing.Province = model.Province;
                existing.Address = model.Address;
                existing.SubscriptionType = model.SubscriptionType;
                existing.ContactPerson = model.ContactPerson;
                existing.ContactEmail = model.ContactEmail;
                existing.ContactPhone = model.ContactPhone;
                existing.SubscriptionStartDate = model.SubscriptionStartDate;
                existing.SubscriptionEndDate = model.SubscriptionEndDate;
                existing.MaxUsers = model.MaxUsers;
                existing.LogoUrl = model.LogoUrl;
                existing.IsActive = model.IsActive;
                existing.IsTwoFactorEnabled = model.IsTwoFactorEnabled; // ✅ NEW: Update Two-Factor setting

                // Audit fields
                existing.UpdatedDate = DateTime.Now;
                existing.UpdatedBy = GetCurrentUserId();

                // ✅ ALWAYS regenerate token on edit (unless blocked)
                string newJwtToken = null;
                if (!existing.IsBlocked)
                {
                    newJwtToken = GenerateJwtToken(existing);
                    existing.FbrSecurityToken = newJwtToken;
                    existing.TokenExpiryDate = existing.SubscriptionEndDate ?? DateTime.UtcNow.AddDays(_jwtExpiryDays);

                    Console.WriteLine($"✅ New JWT Token generated for subscription {id}");
                    Console.WriteLine($"   Token Expiry: {existing.TokenExpiryDate}");
                }

                // ✅ Update SubscriptionSettings with SAME token and other fields
                if (existing.SubscriptionSetting != null)
                {
                    bool settingsNeedUpdate = addressChanged || provinceChanged ||
                                             businessNameChanged || ntnChanged || !string.IsNullOrEmpty(newJwtToken);

                    if (settingsNeedUpdate)
                    {
                        // ✅ CRITICAL: Use the SAME JWT token
                        if (!string.IsNullOrEmpty(newJwtToken))
                        {
                            existing.SubscriptionSetting.FbrToken = newJwtToken;
                        }

                        existing.SubscriptionSetting.SellerNTN = existing.NtnCnic;
                        existing.SubscriptionSetting.SellerBusinessName = existing.BusinessName ?? existing.CompanyName;
                        existing.SubscriptionSetting.SellerAddress = existing.Address;
                        existing.SubscriptionSetting.SellerProvince = existing.Province;
                        existing.SubscriptionSetting.UpdatedDate = DateTime.Now;
                        existing.SubscriptionSetting.UpdatedBy = GetCurrentUserId()?.ToString() ?? "System";

                        _context.SubscriptionSettings.Update(existing.SubscriptionSetting);

                        Console.WriteLine($"✅ Settings updated for subscription {id}");
                        Console.WriteLine($"   SellerNTN: {existing.SubscriptionSetting.SellerNTN}");
                        Console.WriteLine($"   SellerBusinessName: {existing.SubscriptionSetting.SellerBusinessName}");
                        Console.WriteLine($"   SellerAddress: {existing.SubscriptionSetting.SellerAddress}");
                        Console.WriteLine($"   SellerProvince: {existing.SubscriptionSetting.SellerProvince}");
                        Console.WriteLine($"   FbrToken synced: {!string.IsNullOrEmpty(newJwtToken)}");
                    }
                }

                _context.Subscriptions.Update(existing);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Subscription updated successfully! Token regenerated and synced.";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error updating subscription: {ex.Message}");
                TempData["Error"] = "Failed to update subscription: " + ex.Message;
                return View(model);
            }
        }


        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id, string? blockReason)
        {
            var subscription = await _context.Subscriptions
                .Include(s => s.SubscriptionSetting)  // ✅ Include settings
                .FirstOrDefaultAsync(s => s.SubscriptionId == id);

            if (subscription == null)
                return NotFound();

            subscription.IsBlocked = !subscription.IsBlocked;
            subscription.UpdatedDate = DateTime.Now;
            subscription.UpdatedBy = GetCurrentUserId();

            if (subscription.IsBlocked)
            {
                // ✅ Blocking: Remove tokens from both places
                subscription.BlockReason = blockReason ?? "Blocked by SuperAdmin";
                subscription.FbrSecurityToken = null;
                subscription.TokenExpiryDate = null;

                // ✅ Also clear token from settings
                if (subscription.SubscriptionSetting != null)
                {
                    subscription.SubscriptionSetting.FbrToken = null;
                    subscription.SubscriptionSetting.UpdatedDate = DateTime.Now;
                    subscription.SubscriptionSetting.UpdatedBy = GetCurrentUserId()?.ToString() ?? "System";
                    _context.SubscriptionSettings.Update(subscription.SubscriptionSetting);
                }

                Console.WriteLine($"🚫 Subscription {id} blocked - Tokens removed from both places");
            }
            else
            {
                // ✅ Unblocking: Regenerate token in both places
                subscription.BlockReason = null;
                var newJwtToken = GenerateJwtToken(subscription);
                subscription.FbrSecurityToken = newJwtToken;
                subscription.TokenExpiryDate = subscription.SubscriptionEndDate ?? DateTime.UtcNow.AddDays(_jwtExpiryDays);

                // ✅ Also update token in settings
                if (subscription.SubscriptionSetting != null)
                {
                    subscription.SubscriptionSetting.FbrToken = newJwtToken;
                    subscription.SubscriptionSetting.UpdatedDate = DateTime.Now;
                    subscription.SubscriptionSetting.UpdatedBy = GetCurrentUserId()?.ToString() ?? "System";
                    _context.SubscriptionSettings.Update(subscription.SubscriptionSetting);
                }

                Console.WriteLine($"✅ Subscription {id} activated - New token generated in both places");
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = subscription.IsBlocked
                ? "Subscription blocked successfully!"
                : "Subscription activated successfully!";

            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var subscription = await _context.Subscriptions.FindAsync(id);
            if (subscription == null)
            {
                TempData["Error"] = "Subscription not found!";
                return RedirectToAction(nameof(Index));
            }

            return View(subscription);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int SubscriptionId)
        {
            var subscription = await _context.Subscriptions
                .Include(s => s.SubscriptionSetting) // include related settings
                .FirstOrDefaultAsync(s => s.SubscriptionId == SubscriptionId);

            if (subscription == null)
            {
                TempData["Error"] = "Subscription not found!";
                return RedirectToAction(nameof(Index));
            }

            // Check if subscription has users
            var hasUsers = await _context.Users.AnyAsync(u => u.SubscriptionId == SubscriptionId);
            if (hasUsers)
            {
                TempData["Error"] = "Cannot delete subscription with existing users! Please remove users first.";
                return RedirectToAction(nameof(Index));
            }

            // Check if subscription has settings
            if (subscription.SubscriptionSetting !=null)
            {
                TempData["Error"] = "Cannot delete subscription with related settings! Please remove settings first.";
                return RedirectToAction(nameof(Dashboard));
            }

            _context.Subscriptions.Remove(subscription);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Subscription deleted successfully!";
            return RedirectToAction(nameof(Dashboard));
        }



        // ==========================================
        // FIXED: SubscriptionToken Action
        // Now uses SAME JWT token for both places
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> SubscriptionToken(int id)
        {
            // 1. Fetch subscription
            var subscription = await _context.Subscriptions
                .Include(s => s.SubscriptionSetting)
                .FirstOrDefaultAsync(s => s.SubscriptionId == id);

            if (subscription == null)
                return NotFound();

            // 2. Generate token if not exists OR regenerate if needed
            if (subscription.SubscriptionSetting == null)
            {
                // ✅ FIXED: Use the SAME JWT token from subscription
                var setting = new SubscriptionSetting
                {
                    SubscriptionId = subscription.SubscriptionId,
                    FbrBaseUrl = "https://localhost:7118/api/fbrmock",
                    // ✅ CRITICAL FIX: Use the JWT token from subscription, NOT a new GUID
                    FbrToken = subscription.FbrSecurityToken ?? GenerateJwtToken(subscription),
                    SellerNTN = subscription.NtnCnic,
                    SellerBusinessName = subscription.BusinessName ?? subscription.CompanyName,
                    SellerAddress = subscription.Address,
                    SellerProvince = subscription.Province,
                    CreatedBy = User.Identity?.Name ?? "System",
                    CreatedDate = DateTime.Now,
                    IsActive = true
                };

                _context.SubscriptionSettings.Add(setting);

                // ✅ If subscription didn't have token yet, save it back
                if (string.IsNullOrEmpty(subscription.FbrSecurityToken))
                {
                    subscription.FbrSecurityToken = setting.FbrToken;
                    subscription.TokenExpiryDate = subscription.SubscriptionEndDate ?? DateTime.UtcNow.AddDays(_jwtExpiryDays);
                    _context.Subscriptions.Update(subscription);
                }

                await _context.SaveChangesAsync();
                subscription.SubscriptionSetting = setting;

                Console.WriteLine($"✅ Settings created with JWT token for subscription {id}");
                Console.WriteLine($"   Token (first 20 chars): {setting.FbrToken?.Substring(0, Math.Min(20, setting.FbrToken.Length))}...");
            }
            else
            {
                // ✅ Sync token if they're different
                if (subscription.SubscriptionSetting.FbrToken != subscription.FbrSecurityToken)
                {
                    subscription.SubscriptionSetting.FbrToken = subscription.FbrSecurityToken;
                    subscription.SubscriptionSetting.UpdatedDate = DateTime.Now;
                    subscription.SubscriptionSetting.UpdatedBy = User.Identity?.Name ?? "System";

                    _context.SubscriptionSettings.Update(subscription.SubscriptionSetting);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"✅ Token synced for subscription {id}");
                }
            }

            // 3. Pass to view
            return View(subscription);
        }
        [HttpGet]
        public async Task<IActionResult> SettingIndex(string? searchName, string? searchStatus, int page = 1, int pageSize = 10)
        {
            var query = _context.SubscriptionSettings.Include(s => s.Subscription).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(s => s.Subscription.CompanyName.Contains(searchName));
            }

            if (!string.IsNullOrWhiteSpace(searchStatus))
            {
                switch (searchStatus.ToLower())
                {
                    case "active":
                        query = query.Where(s => s.IsActive);
                        break;
                    case "inactive":
                        query = query.Where(s => !s.IsActive);
                        break;
                }
            }

            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            var model = await query
                .OrderByDescending(s => s.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.PageNumber = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchName = searchName;
            ViewBag.SearchStatus = searchStatus;

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBySubscriptionId(int subscriptionId)
        {
            try
            {
                var setting = await _context.SubscriptionSettings
                    .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

                if (setting == null)
                    return NotFound("No setting found for this subscription.");

                // Optional: clear token
                var subscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
                if (subscription != null)
                {
                    subscription.FbrSecurityToken = null;
                    _context.Subscriptions.Update(subscription);
                }

                _context.SubscriptionSettings.Remove(setting);
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting subscription setting: {ex.Message}");
            }
        }

        // ==================== POSTED INVOICES VIEW ====================
        // GET: /SuperAdmin/PostedInvoices
        [HttpGet]
        public async Task<IActionResult> PostedInvoices(int? subscriptionId = null, string? search = null, int page = 1, int pageSize = 10)
        {
            try
            {
                // ✅ Load all subscriptions for dropdown filter
                var allSubscriptions = await _context.Subscriptions
                    .Select(s => new { s.SubscriptionId, s.CompanyName })
                    .OrderBy(s => s.CompanyName)
                    .ToListAsync();

                ViewBag.AllSubscriptions = allSubscriptions;
                ViewBag.SubscriptionId = subscriptionId;

                // Base query - ONLY Posted status invoices
                var query = _context.Invoices
                    .Include(i => i.Subscription)
                    .Include(i => i.Buyer)
                    .Include(i => i.Items)
                    .Where(i => i.Status == "Posted"); // ✅ CRITICAL: Only Posted status

                // ✅ Filter by subscription if selected
                if (subscriptionId.HasValue && subscriptionId.Value > 0)
                {
                    query = query.Where(i => i.SubscriptionId == subscriptionId.Value);
                }

                // 🔍 Filter by search text
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(i =>
                        (i.InvoiceRefNo ?? "").Contains(search) ||
                        (i.FBRInvoiceNo ?? "").Contains(search) ||
                        (i.Buyer != null ? i.Buyer.BuyerBusinessName : "").Contains(search) ||
                        (i.Subscription != null ? i.Subscription.CompanyName : "").Contains(search));
                }

                // Pagination
                var totalRecords = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

                var invoices = await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // ViewBag
                ViewBag.PageNumber = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRecords = totalRecords;
                ViewBag.TotalPages = totalPages;
                ViewBag.SearchTerm = search;

                return View(invoices);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load posted invoices: " + ex.Message;
                return View(new List<Invoice>());
            }
        }

        // ==================== POSTED INVOICE DETAILS ====================
        // GET: /SuperAdmin/PostedInvoiceDetails/{id}
        [HttpGet]
        public async Task<IActionResult> PostedInvoiceDetails(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Items)
                    .Include(i => i.Subscription)
                    .FirstOrDefaultAsync(i => i.InvoiceId == id);

                if (invoice == null)
                {
                    TempData["Error"] = "Invoice not found!";
                    return RedirectToAction("PostedInvoices");
                }

                // ✅ Optional: Verify it's actually Posted status
                if (invoice.Status != "Posted")
                {
                    TempData["Warning"] = "This invoice is not in Posted status!";
                }

                ViewBag.Subscription = invoice.Subscription;

                // ✅ Use Invoice/Details view (read-only)
                return View("~/Views/Invoice/Details.cshtml", invoice);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading invoice details: " + ex.Message;
                return RedirectToAction("PostedInvoices");
            }
        }
        // ==================== SUPERADMIN: VIEW INVOICES FOR A SUBSCRIPTION ====================
        // GET: /SuperAdmin/SubscriptionInvoices
        [HttpGet]
        public async Task<IActionResult> SubscriptionInvoices(int subscriptionId, string status = null, string search = null, int page = 1, int pageSize = 10)
        {
            // Filter invoices by subscription
            var query = _context.Invoices
                .Include(i => i.Buyer)
                .Include(i => i.Items)
                .Where(i => i.SubscriptionId == subscriptionId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(i => i.Status == status);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(i =>
                    (i.InvoiceRefNo ?? "").Contains(search) ||
                    (i.FBRInvoiceNo ?? "").Contains(search) ||
                    (i.Buyer != null ? i.Buyer.BuyerBusinessName : "").Contains(search)
                );

            // Pagination
            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            var invoices = await query
                .OrderByDescending(i => i.InvoiceDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Pass ViewBag for filters/pagination
            ViewBag.SubscriptionId = subscriptionId;
            ViewBag.CurrentStatus = status;
            ViewBag.SearchTerm = search;
            ViewBag.PageNumber = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = totalRecords;

            // Use same Invoice/Index.cshtml view
            return View("~/Views/Invoice/Index.cshtml", invoices);
        }



        private int? GetCurrentUserId()
        {
            // Get from claims if using authentication
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return 1; // Default SuperAdmin ID
        }
        
    }
}