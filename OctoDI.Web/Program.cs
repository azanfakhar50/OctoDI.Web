using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OctoDI.Web.Hubs;
using OctoDI.Web.Middleware;
using OctoDI.Web.Models.DatabaseModels;
using OctoDI.Web.Services;
using OfficeOpenXml;
using YourProject.Services;
using OpenAI;
using OpenAI.Chat;

// ✅ SET LICENSE CONTEXT FOR EPPlus
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var builder = WebApplication.CreateBuilder(args);

// ==================== SERVICES ====================

// HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Controllers + NewtonsoftJson
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson();

// ✅ HttpClient for SimAI (configured with BaseUrl and API key)
builder.Services.AddHttpClient<ISimAiService, SimAiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["SimAI:BaseUrl"]);
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", builder.Configuration["SimAI:ApiKey"]);
});

// Scoped Services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IInvoiceLogService, InvoiceLogService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Hosted Service
builder.Services.AddHostedService<TokenExpiryService>();

// SignalR
builder.Services.AddSignalR();

// DbContexts
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<InvoiceLoggingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("InvoiceLoggingConnection")));

// Session Configuration
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "OctoDIAuth";
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
    });

var app = builder.Build();

// ==================== MIDDLEWARE ====================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseMiddleware<SubscriptionValidationMiddleware>();
app.UseAuthorization();

// ==================== ENDPOINTS ====================

// SignalR Hub
app.MapHub<SubscriptionHub>("/subscriptionHub");

// MVC Controllers & Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapControllers();

app.Run();
