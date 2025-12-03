using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OctoDI.Web.Models.DatabaseModels;
using OctoDI.Web.Models.ViewModels;
using OctoDI.Web.Services;
using System.Security.Claims;
using YourProject.Services;

namespace OctoDI.Web.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;
        private readonly IAuditService _auditService;

        public AccountController(ApplicationDbContext context, IOtpService otpService, IAuditService auditService)
        {
            _context = context;
            _otpService = otpService;
            _auditService = auditService;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            Console.WriteLine("========== LOGIN PAGE ACCESS ==========");
            Console.WriteLine($"🔍 Is Authenticated: {User?.Identity?.IsAuthenticated}");
            Console.WriteLine($"🔍 User Role: {User?.FindFirst(ClaimTypes.Role)?.Value}");

            if (User?.Identity?.IsAuthenticated == true)
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                Console.WriteLine($"✅ User already authenticated! Role: {userRole}");

                return userRole switch
                {
                    "SuperAdmin" => RedirectToAction("Index", "SuperAdmin"),
                    "SubscriptionAdmin" => RedirectToAction("Index", "SubscriptionAdmin"),
                    _ => RedirectToAction("Index", "Invoice"),
                };
            }

            Console.WriteLine("🔓 Showing login page");
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            Console.WriteLine("========== LOGIN ATTEMPT ==========");
            Console.WriteLine($"📧 Username: {model.Username}");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("❌ ModelState Invalid");
                return View(model);
            }

            try
            {
                // 1️⃣ Fetch user from DB
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == model.Username && u.IsActive);

                if (user == null)
                {
                    Console.WriteLine("❌ User not found or inactive");
                    ViewBag.Error = "Invalid username or password";
                    return View(model);
                }

                Console.WriteLine($"✅ User: {user.Username}, Role: {user.UserRole}");

                // 2️⃣ Verify password
                var passwordHasher = new PasswordHasher<User>();
                var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

                if (result == PasswordVerificationResult.Failed)
                {
                    Console.WriteLine("❌ Password verification failed");
                    ViewBag.Error = "Invalid username or password";
                    return View(model);
                }

                // ============================================================
                // 3️⃣ SUPERADMIN LOGIN
                // ============================================================
                if (user.UserRole == "SuperAdmin")
                {
                    Console.WriteLine("🔐 SuperAdmin login - Checking 2FA...");

                    if (user.IsTwoFactorEnabled)
                    {
                        Console.WriteLine("📨 SuperAdmin 2FA Enabled → Sending OTP");

                        var otpCode = _otpService.GenerateOtp();

                        var otpEntry = new TwoFactorOtp
                        {
                            UserId = user.UserId,
                            Code = otpCode,
                            ExpiryTime = DateTime.Now.AddMinutes(5),
                            IsUsed = false
                        };

                        _context.TwoFactorOtps.Add(otpEntry);
                        await _context.SaveChangesAsync();

                        await _otpService.SendOtpEmailAsync(user.Email, otpCode);

                        return RedirectToAction("VerifyOtp", new { userId = user.UserId });
                    }

                    Console.WriteLine("⚡ SuperAdmin 2FA Disabled → Logging in directly...");

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.UserRole),
                        new Claim("UserId", user.UserId.ToString()),
                        new Claim(ClaimTypes.Email, user.Email ?? "")
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal,
                        new AuthenticationProperties
                        {
                            IsPersistent = model.RememberMe,
                            ExpiresUtc = DateTime.UtcNow.AddHours(4)
                        });

                    HttpContext.Session.SetInt32("UserId", user.UserId);
                    HttpContext.Session.SetString("Username", user.Username);
                    HttpContext.Session.SetString("UserRole", user.UserRole);

                    user.LastLoginDate = DateTime.Now;
                    await _context.SaveChangesAsync();

                    Console.WriteLine("✅ SuperAdmin signed in successfully!");

                    // ✅ FIXED: Pass userId and userName explicitly
                    await _auditService.LogAsync("Login", user.UserRole, null, null, user.UserId.ToString(), user.Username);

                    return RedirectToAction("Dashboard", "SuperAdmin");
                }

                // ============================================================
                // 4️⃣ SUBSCRIPTION ADMIN / SUBSCRIPTION USER
                // ============================================================
                if (user.UserRole == "SubscriptionAdmin" || user.UserRole == "SubscriptionUser")
                {
                    Console.WriteLine($"📋 Checking subscription for user (SubscriptionId: {user.SubscriptionId})");

                    var subscription = await _context.Subscriptions
                        .FirstOrDefaultAsync(s => s.SubscriptionId == user.SubscriptionId);

                    if (subscription == null)
                    {
                        Console.WriteLine("❌ Subscription not found");
                        ViewBag.Error = "Your subscription not found. Please contact SuperAdmin.";
                        return View(model);
                    }

                    if (!subscription.IsActive)
                    {
                        Console.WriteLine("❌ Subscription inactive");
                        ViewBag.Error = "Your subscription is inactive. Please contact SuperAdmin.";
                        return View(model);
                    }

                    if (subscription.IsBlocked)
                    {
                        Console.WriteLine("❌ Subscription blocked");
                        ViewBag.Error = "Your subscription is blocked. Please contact SuperAdmin.";
                        return View(model);
                    }

                    if (subscription.SubscriptionEndDate.HasValue && subscription.SubscriptionEndDate.Value < DateTime.Now)
                    {
                        Console.WriteLine($"❌ Subscription expired: {subscription.SubscriptionEndDate.Value}");
                        ViewBag.Error = "Your subscription has expired. Please renew to continue.";
                        return View(model);
                    }

                    if (subscription.TokenExpiryDate.HasValue && subscription.TokenExpiryDate.Value < DateTime.Now)
                    {
                        Console.WriteLine($"❌ Token expired: {subscription.TokenExpiryDate.Value}");
                        ViewBag.Error = "Your subscription token has expired. Please contact SuperAdmin.";
                        return View(model);
                    }

                    Console.WriteLine("✅ All subscription checks passed");

                    Console.WriteLine($"🔐 2FA Check:");
                    Console.WriteLine($"   - Subscription.IsTwoFactorEnabled: {subscription.IsTwoFactorEnabled}");
                    Console.WriteLine($"   - User.IsTwoFactorEnabled: {user.IsTwoFactorEnabled}");

                    bool require2FA = subscription.IsTwoFactorEnabled && user.IsTwoFactorEnabled;

                    if (require2FA)
                    {
                        Console.WriteLine("✅ BOTH 2FA enabled → Sending OTP");

                        var otpCode = _otpService.GenerateOtp();

                        var otpEntry = new TwoFactorOtp
                        {
                            UserId = user.UserId,
                            Code = otpCode,
                            ExpiryTime = DateTime.Now.AddMinutes(5),
                            IsUsed = false
                        };

                        _context.TwoFactorOtps.Add(otpEntry);
                        await _context.SaveChangesAsync();

                        await _otpService.SendOtpEmailAsync(user.Email, otpCode);

                        return RedirectToAction("VerifyOtp", new { userId = user.UserId });
                    }
                    else
                    {
                        if (!subscription.IsTwoFactorEnabled && user.IsTwoFactorEnabled)
                        {
                            Console.WriteLine("⚠️ Subscription 2FA Disabled, User 2FA Enabled → Bypassing 2FA");
                        }
                        else if (subscription.IsTwoFactorEnabled && !user.IsTwoFactorEnabled)
                        {
                            Console.WriteLine("⚠️ Subscription 2FA Enabled, User 2FA Disabled → Bypassing 2FA");
                        }
                        else
                        {
                            Console.WriteLine("⚠️ Both 2FA Disabled → Bypassing 2FA");
                        }
                    }

                    var claimsSubscription = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.UserRole),
                        new Claim("UserId", user.UserId.ToString()),
                        new Claim("SubscriptionId", subscription.SubscriptionId.ToString()),
                        new Claim("SubscriptionType", subscription.SubscriptionType ?? ""),
                         new Claim("CompanyName", subscription.CompanyName ?? ""),
                        new Claim(ClaimTypes.Email, user.Email ?? "")
                    };

                    var identitySubscription = new ClaimsIdentity(claimsSubscription, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principalSubscription = new ClaimsPrincipal(identitySubscription);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principalSubscription,
                        new AuthenticationProperties
                        {
                            IsPersistent = model.RememberMe,
                            ExpiresUtc = DateTime.UtcNow.AddHours(4)
                        });

                    HttpContext.Session.SetInt32("UserId", user.UserId);
                    HttpContext.Session.SetString("Username", user.Username);
                    HttpContext.Session.SetString("UserRole", user.UserRole);
                    HttpContext.Session.SetInt32("SubscriptionId", subscription.SubscriptionId);

                    user.LastLoginDate = DateTime.Now;
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"✅ User signed in successfully! Redirecting to: {user.UserRole}");

                    // ✅ FIXED: Pass userId and userName explicitly
                    await _auditService.LogAsync("Login", user.UserRole, subscription.SubscriptionId, subscription.CompanyName, user.UserId.ToString(), user.Username);

                    return user.UserRole switch
                    {
                        "SubscriptionAdmin" => RedirectToAction("Index", "SubscriptionAdmin"),
                        "SubscriptionUser" => RedirectToAction("UserDashboard", "SubscriptionAdmin"),
                        _ => RedirectToAction("UserDashboard", "SubscriptionAdmin"),
                    };
                }

                // Fallback
                ViewBag.Error = "User role not recognized. Please contact SuperAdmin.";
                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EXCEPTION: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
                ViewBag.Error = "An error occurred during login. Please try again.";
                return View(model);
            }
        }

        // GET: /Account/VerifyOtp
        [HttpGet]
        public IActionResult VerifyOtp(int userId)
        {
            return View(new VerifyOtpViewModel { UserId = userId });
        }

        // POST: /Account/VerifyOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 1️⃣ Check OTP
            var otpEntry = await _context.TwoFactorOtps
                .FirstOrDefaultAsync(o => o.UserId == model.UserId && o.Code == model.Code && !o.IsUsed);

            if (otpEntry == null || otpEntry.ExpiryTime < DateTime.Now)
            {
                ModelState.AddModelError("", "Invalid or expired OTP code.");
                return View(model);
            }

            // Mark OTP as used
            otpEntry.IsUsed = true;
            await _context.SaveChangesAsync();

            // 2️⃣ Fetch user (with subscription if exists)
            var user = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.UserId == model.UserId);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(model);
            }

            // 3️⃣ Prepare claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.UserRole),
                new Claim("UserId", user.UserId.ToString())
            };

            // Add subscription claims only if user has a subscription
            if (user.UserRole != "SuperAdmin" && user.Subscription != null)
            {
                claims.Add(new Claim("SubscriptionId", user.Subscription.SubscriptionId.ToString()));
                claims.Add(new Claim("SubscriptionType", user.Subscription.SubscriptionType ?? ""));
                HttpContext.Session.SetInt32("SubscriptionId", user.Subscription.SubscriptionId);
            }

            // 4️⃣ Create identity & principal
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTime.UtcNow.AddHours(4)
                });

            // 5️⃣ Set session values
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("UserRole", user.UserRole);

            // 6️⃣ Update last login
            user.LastLoginDate = DateTime.Now;
            await _context.SaveChangesAsync();

            // ✅ Log the audit after OTP verification
            if (user.UserRole == "SuperAdmin")
            {
                await _auditService.LogAsync("Login", user.UserRole, null, null, user.UserId.ToString(), user.Username);
            }
            else if (user.Subscription != null)
            {
                await _auditService.LogAsync("Login", user.UserRole, user.Subscription.SubscriptionId, user.Subscription.CompanyName, user.UserId.ToString(), user.Username);
            }

            // 7️⃣ Redirect based on role
            return user.UserRole switch
            {
                "SuperAdmin" => RedirectToAction("Dashboard", "SuperAdmin"),
                "SubscriptionAdmin" => RedirectToAction("Index", "SubscriptionAdmin"),
                _ => RedirectToAction("Index", "Invoice"),
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            Console.WriteLine("========== LOGOUT ==========");

            // ✅ SAVE user data BEFORE signing out
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User?.Identity?.Name;
            var userRole = User?.FindFirst(ClaimTypes.Role)?.Value;
            var subscriptionIdValue = User?.FindFirst("SubscriptionId")?.Value;

            int? subscriptionId = null;
            if (!string.IsNullOrEmpty(subscriptionIdValue) && int.TryParse(subscriptionIdValue, out int subId))
            {
                subscriptionId = subId;
            }

            // ✅ Now fetch CompanyName from claim directly
            var companyName = User?.FindFirst("CompanyName")?.Value;

            Console.WriteLine($"🚪 Logging out user: {userName}, Role: {userRole}, Company: {companyName}");

            // Clear session
            HttpContext.Session.Clear();

            // Sign out authentication cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            Console.WriteLine("✅ User logged out successfully!");

            // ✅ FIXED: Pass saved values with correct CompanyName
            await _auditService.LogAsync("Logout", userRole, subscriptionId, companyName, userId, userName);

            return RedirectToAction("Login", "Account");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            Console.WriteLine("⛔ Access denied page");
            return View();
        }
    }
}