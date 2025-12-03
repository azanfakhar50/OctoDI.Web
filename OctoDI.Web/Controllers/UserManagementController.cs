using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OctoDI.Web.Models.DatabaseModels;
using OctoDI.Web.Models.ViewModels;

public class UserManagementController : Controller
{
    private readonly ApplicationDbContext _context;
    public UserManagementController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /UserManagement
    public async Task<IActionResult> Index(string searchName, string searchRole, int? searchSubscriptionId)
    {
        var query = _context.Users
            .Include(u => u.Subscription)
            .AsQueryable();

        // Filters
        if (!string.IsNullOrEmpty(searchName))
            query = query.Where(u => u.Username.Contains(searchName)
                                  || u.FirstName.Contains(searchName)
                                  || u.LastName.Contains(searchName));

        if (!string.IsNullOrEmpty(searchRole))
            query = query.Where(u => u.UserRole == searchRole);

        if (searchSubscriptionId.HasValue)
            query = query.Where(u => u.SubscriptionId == searchSubscriptionId.Value);

        var users = await query
            .Select(u => new UserManagementViewModel
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                PhoneNumber = u.PhoneNumber,
                UserRole = u.UserRole,
                IsActive = u.IsActive,
                CreatedDate = u.CreatedDate,
                SubscriptionId = u.Subscription != null ? u.Subscription.SubscriptionId : 0,
                CompanyName = u.Subscription != null ? u.Subscription.SubscriptionName : "-",
                SubscriptionType = u.Subscription != null ? u.Subscription.SubscriptionType : "-"
            })
            .OrderBy(u => u.UserId)
            .ToListAsync();

        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(int id)
    {
        var user = await _context.Users
            .Include(u => u.Subscription)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound();

        var vm = new EditUserViewModel
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            UserRole = user.UserRole,
            SubscriptionId = user.SubscriptionId,
            IsActive = user.IsActive,
            IsTwoFactorEnabled = user.IsTwoFactorEnabled,

            // FIX: Use ToListAsync() instead of ToList()
            Subscriptions = await _context.Subscriptions
                .Select(s => new SelectListItem
                {
                    Value = s.SubscriptionId.ToString(),
                    Text = s.SubscriptionName
                })
                .ToListAsync()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(EditUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            // FIX: Reload subscriptions when validation fails
            await ReloadSubscriptions(model);
            return View(model);
        }

        var user = await _context.Users.FindAsync(model.UserId);
        if (user == null)
            return NotFound();

        user.Username = model.Username;
        user.Email = model.Email;
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.PhoneNumber = model.PhoneNumber;
        user.UserRole = model.UserRole;
        user.SubscriptionId = model.SubscriptionId == 0 ? null : model.SubscriptionId;
        user.IsActive = model.IsActive;
        user.IsTwoFactorEnabled = model.IsTwoFactorEnabled;
        user.UpdatedDate = DateTime.Now;

        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = "User updated successfully!";
            return RedirectToAction("Index");
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await UserExists(model.UserId))
            {
                return NotFound();
            }
            throw;
        }
    }

    // POST: /UserManagement/DeleteUser
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"User {user.Username} deleted successfully.";
        }
        else
        {
            TempData["Error"] = "User not found!";
        }

        return RedirectToAction(nameof(Index));
    }

    // Helper method to reload subscriptions
    private async Task ReloadSubscriptions(EditUserViewModel model)
    {
        model.Subscriptions = await _context.Subscriptions
            .Select(s => new SelectListItem
            {
                Value = s.SubscriptionId.ToString(),
                Text = s.SubscriptionName
            })
            .ToListAsync();
    }

    // Helper method to check if user exists
    private async Task<bool> UserExists(int id)
    {
        return await _context.Users.AnyAsync(e => e.UserId == id);
    }
}