using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.BackgroundServices;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Filters;
using SecureSmartTuitionSystem.Middleware;
using SecureSmartTuitionSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Makes the |DataDirectory| macro used by AttachDbFilename connection strings
// resolve to /App_Data, matching the classic "SQL Server Database (.mdf)"
// workflow (Solution Explorer -> App_Data -> Add New Item -> SQL Server
// Database). ASP.NET Core does NOT wire this up automatically the way
// classic ASP.NET/System.Web did, so it is set explicitly here.
AppContext.SetData("DataDirectory", Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

// --- MVC + global input-validation / CSRF wiring -------------------------------------
// AddControllersWithViews (rather than AddControllers) because Views are
// intentionally out of scope for this deliverable (UI Design excluded per
// the report brief) — the framework registration is left in place so a
// Razor UI can be added later without touching any Controller here.
builder.Services.AddControllersWithViews(options =>
{
    // AuditActionFilter reads the [Audit] attribute wherever a Controller opts in.
    options.Filters.Add<AuditActionFilter>();
});

// System Security Module: CSRF token protection (Ch.2.5 / Table 4.29).
// [ValidateAntiForgeryToken] on every state-changing action pairs with this.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN"; // for SPA/fetch-based clients submitting the token via header
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// System Security Module: session management (Ch.2.5 / Table 4.30).
builder.Services.AddDistributedMemoryCache(); // swap for Redis/SQL Server cache in production
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(builder.Configuration.GetValue("Security:SessionTimeoutMinutes", 20));
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// User Authentication & RBAC Module integration point: registration, login,
// and password-reset endpoints (and the full ASP.NET Core Identity/RBAC
// setup) belong to the teammate's module. This project only *consumes* the
// resulting cookie/claims principal ([Authorize], ClaimTypes.Role,
// ClaimTypes.NameIdentifier — see Filters/CurrentUserExtensions.cs), so a
// minimal cookie scheme is registered here purely so [Authorize] has a
// scheme to check against; replace this block with `AddIdentity<...>()`
// once the two modules are wired together in the shared solution.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Security:SessionTimeoutMinutes", 20));
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/Account/Login";
    });
builder.Services.AddAuthorization();

// --- Database (Ch.2.4 "Database Integration Using SQL Server and Entity Framework Core") ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Options binding -------------------------------------------------------------------
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection("Billing"));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));

// --- Application services ---------------------------------------------------------------
builder.Services.AddScoped<IEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<IDuplicateRecordService, DuplicateRecordService>();
builder.Services.AddScoped<IFeeCalculationService, FeeCalculationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IFileValidationService, FileValidationService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IPaymentGatewayClient, MockPaymentGatewayClient>(); // swap for a real gateway SDK before go-live

// --- Background jobs (Payment & Billing automation, Communication scheduled triggers) ---
builder.Services.AddHostedService<BillingSchedulerService>();
builder.Services.AddHostedService<NotificationSchedulerService>();

var app = builder.Build();

// --- Middleware pipeline -----------------------------------------------------------------
// System Security Module: structured error handling (Ch.2.5 / Table 4.33) - registered
// first so it wraps every other middleware/Controller action below.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // serves message attachments saved under wwwroot/uploads
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Class}/{action=Browse}/{id?}");

app.Run();
