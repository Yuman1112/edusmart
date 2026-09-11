using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Services.Authentication;
using SecureSmartTuitionSystem.Services.Billing;
using SecureSmartTuitionSystem.Settings;
using Stripe;

var builder =
    WebApplication.CreateBuilder(args);

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

/*
 * =====================================================
 * MVC AND RAZOR PAGES
 * =====================================================
 */

builder.Services.AddControllersWithViews();

builder.Services.AddRazorPages();

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

app.UseRouting();

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