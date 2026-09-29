using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Middleware;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.Services.Billing;
using SecureSmartTuitionSystem.Settings;
using Stripe;
using System.Text;
using System.Threading.RateLimiting;

var builder =
    WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

/*
 * =====================================================
 * DATABASE
 * =====================================================
 */

string connectionString =
    builder.Configuration
        .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' " +
        "was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
        options.UseSqlServer(connectionString));

/*
 * =====================================================
 * IDENTITY AND SECURITY
 * =====================================================
 */

builder.Services
    .AddDefaultIdentity<ApplicationUser>(
        options =>
        {
            options.SignIn.RequireConfirmedAccount =
                false;

            options.Password.RequireDigit =
                true;

            options.Password.RequireLowercase =
                true;

            options.Password.RequireUppercase =
                true;

            options.Password.RequireNonAlphanumeric =
                true;

            options.Password.RequiredLength =
                8;

            options.Lockout.MaxFailedAccessAttempts =
                5;

            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(15);

            options.Lockout.AllowedForNewUsers =
                true;
        })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".EduSmart.Identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(2);
});

/*
 * =====================================================
 * MVC AND RAZOR PAGES
 * =====================================================
 */

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(
        new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddRazorPages();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter(
        "LoginPolicy",
        limiter =>
        {
            limiter.PermitLimit = 5;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
            limiter.AutoReplenishment = true;
        });

    options.AddFixedWindowLimiter(
        "PublicFormPolicy",
        limiter =>
        {
            limiter.PermitLimit = 20;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
            limiter.AutoReplenishment = true;
        });

    options.AddFixedWindowLimiter(
        "PaymentPolicy",
        limiter =>
        {
            limiter.PermitLimit = 10;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
            limiter.AutoReplenishment = true;
        });
});

/*
 * =====================================================
 * SESSION
 * =====================================================
 */

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(
    options =>
    {
        options.IdleTimeout =
            TimeSpan.FromMinutes(30);

        options.Cookie.Name =
            ".EduSmart.Session";

        options.Cookie.HttpOnly =
            true;

        options.Cookie.IsEssential =
            true;

        options.Cookie.SecurePolicy =
            CookieSecurePolicy.Always;

        options.Cookie.SameSite =
            SameSiteMode.Lax;
    });

/*
 * =====================================================
 * APPLICATION SERVICES
 * =====================================================
 */

builder.Services.AddScoped<
    AccountProvisioningService>();

builder.Services.AddScoped<
    IBillingService,
    SecureSmartTuitionSystem.Services.Billing.BillingService>();

builder.Services.AddScoped<
    IPaymentCompletionService,
    PaymentCompletionService>();

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(
        EmailSettings.SectionName));

builder.Services.AddScoped<
    IFirstPaymentEmailService,
    FirstPaymentEmailService>();

builder.Services.AddScoped<
    IParentAccountSetupEmailService,
    ParentAccountSetupEmailService>();

builder.Services.AddScoped<
    IStudentAccountSetupEmailService,
    StudentAccountSetupEmailService>();

/*
 * =====================================================
 * STRIPE TEST MODE
 * =====================================================
 */

builder.Services.Configure<StripeSettings>(
    builder.Configuration.GetSection(
        StripeSettings.SectionName));

builder.Services.AddSingleton<Stripe.StripeClient>(
    serviceProvider =>
    {
        var stripeSettings =
            serviceProvider
                .GetRequiredService<
                    IOptions<StripeSettings>>()
                .Value;

        if (string.IsNullOrWhiteSpace(
                stripeSettings.SecretKey))
        {
            throw new InvalidOperationException(
                "Stripe test secret key is not configured.");
        }

        if (!stripeSettings.SecretKey.StartsWith(
                "sk_test_",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Only a Stripe test secret key is allowed.");
        }

        return new Stripe.StripeClient(
            stripeSettings.SecretKey);
    });

/*
 * =====================================================
 * BUILD APPLICATION
 * =====================================================
 */

var app = builder.Build();

/*
 * =====================================================
 * DATABASE INITIALISATION
 * =====================================================
 */

await DbInitializer.SeedRolesAsync(
    app.Services,
    seedBasicAccounts:
        app.Environment.IsDevelopment());

/*
 * =====================================================
 * ERROR HANDLING
 * =====================================================
 */

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

/*
 * =====================================================
 * HTTP PIPELINE
 * =====================================================
 */

app.UseHttpsRedirection();

app.UseMiddleware<SecurityMonitoringMiddleware>();

app.UseRouting();

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd(
        "X-Content-Type-Options",
        "nosniff");

    context.Response.Headers.TryAdd(
        "X-Frame-Options",
        "DENY");

    context.Response.Headers.TryAdd(
        "Referrer-Policy",
        "strict-origin-when-cross-origin");

    context.Response.Headers.TryAdd(
        "Permissions-Policy",
        "camera=(), microphone=(), geolocation=()");

    context.Response.Headers.TryAdd(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "img-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline' " +
            "https://cdn.jsdelivr.net; " +
        "script-src 'self' 'unsafe-inline'; " +
        "font-src 'self' data: " +
            "https://cdn.jsdelivr.net; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self' https://checkout.stripe.com;");

    await next();
});

app.UseRateLimiter();

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

/*
 * =====================================================
 * STATIC FILES
 * =====================================================
 */

app.MapStaticAssets();

/*
 * =====================================================
 * ROUTES
 * =====================================================
 */

app.MapControllerRoute(
        name: "default",
        pattern:
            "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

/*
 * =====================================================
 * RUN APPLICATION
 * =====================================================
 */

app.Run();
