using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.Authentication.Cookies;
using SecureSmartTuitionSystem.Services.Security;
using System.Security.Claims;
using System.Security.Cryptography;
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
using SecureSmartTuitionSystem.Services.Communication;
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

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditLogIntegrity>();
builder.Services.AddSingleton<FinancialFieldProtection>();
builder.Services.AddSingleton<PaymentCallbackTokenService>();
builder.Services.AddSingleton<SecurityEventService>();
builder.Services.Configure<ChatbotSettings>(builder.Configuration.GetSection(ChatbotSettings.SectionName));
builder.Services.AddHttpClient<SecureSmartTuitionSystem.Services.Communication.ChatbotAIService>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<IGuestEnquiryEmailSender, GuestEnquiryEmailSender>();
builder.Services.AddScoped<SecureSmartTuitionSystem.Services.Communication.ChatbotKnowledgeStore>();
builder.Services.AddScoped<SecureSmartTuitionSystem.Services.Communication.ChatbotContextService>();
builder.Services.AddScoped<SecureSmartTuitionSystem.Services.Communication.ChatbotAssistantService>();
builder.Services.AddSingleton<SecureSmartTuitionSystem.Services.Communication.ChatbotSessionService>();
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection(SecuritySettings.SectionName));
builder.Services.Configure<SecurityExplanationAISettings>(builder.Configuration.GetSection(SecurityExplanationAISettings.SectionName));
builder.Services.AddHttpClient<SecurityExplanationAI>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHostedService<SecurityAlertWorker>();
builder.Services.AddHostedService<BillExpiryWorker>();
builder.Services.AddScoped<SecureSmartTuitionSystem.Services.Billing.MonthlyBillingService>();
builder.Services.AddScoped<SecureSmartTuitionSystem.Services.Billing.MonthlyPaymentService>();
builder.Services.AddScoped<SecureSmartTuitionSystem.Filters.BillingViewOnlyFilter>();
builder.Services.AddHostedService<SecureSmartTuitionSystem.Services.Billing.MonthlyBillingWorker>();
builder.Services.AddScoped<SecureSmartTuitionSystem.Services.Enrollment.ClassScheduleService>();
builder.Services.AddScoped<SecureSmartTuitionSystem.Services.Enrollment.LearningAccessService>();
builder.Services.AddScoped<SecureSmartTuitionSystem.Services.Enrollment.EnrollmentExitService>();
builder.Services.AddSingleton<SecureSmartTuitionSystem.Services.Enrollment.AdminWithdrawalConfirmation>();
builder.Services.AddHostedService<SecureSmartTuitionSystem.Services.Enrollment.EnrollmentLifecycleWorker>();

var protection = builder.Services.AddDataProtection().UseCryptographicAlgorithms(
    new AuthenticatedEncryptorConfiguration {
        EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
        ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
    });
// Leave the existing key ring location unchanged by default so old messages remain readable.
string? keyRingPath = builder.Configuration["Security:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
string? certificatePath = builder.Configuration["Security:KeyCertificatePath"];
if (!string.IsNullOrWhiteSpace(certificatePath))
{
    var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
        certificatePath, builder.Configuration["Security:KeyCertificatePassword"],
        System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.EphemeralKeySet);
    protection.ProtectKeysWithCertificate(certificate);
}
else if (!string.IsNullOrWhiteSpace(keyRingPath) && OperatingSystem.IsWindows())
    protection.ProtectKeysWithDpapi();

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
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, SecurityClaimsPrincipalFactory>();
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    // Revoked sessions are rejected on their next request, including background polling.
    options.ValidationInterval = TimeSpan.Zero;
    options.OnRefreshingPrincipal = context =>
    {
        var previousActivity = context.CurrentPrincipal?.FindFirst("EduSmart.LastActivityUtc");
        if (context.NewPrincipal?.Identity is ClaimsIdentity identity)
        {
            foreach (var claim in identity.FindAll("EduSmart.LastActivityUtc").ToList()) identity.RemoveClaim(claim);
            if (previousActivity != null) identity.AddClaim(previousActivity);
        }
        return Task.CompletedTask;
    };
});
foreach (string scheme in new[] { IdentityConstants.TwoFactorUserIdScheme, IdentityConstants.TwoFactorRememberMeScheme })
{
    builder.Services.Configure<CookieAuthenticationOptions>(scheme, options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
}

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
    options.Filters.AddService<SecureSmartTuitionSystem.Filters.BillingViewOnlyFilter>();
    options.Filters.Add(
        new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddRazorPages();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("security-explanation", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions {
                PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
            }));

    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("GuestEnquiryPolicy", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true }));

    foreach (var policy in new[] { ("LoginPolicy", 5), ("PublicFormPolicy", 20), ("ChatbotPolicy", 6), ("PaymentPolicy", 10) })
    {
        int permits = policy.Item2;
        options.AddPolicy(policy.Item1, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions {
                PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                AutoReplenishment = true
            }));
    }
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

builder.Services.AddScoped<
    ITutorAccountSetupEmailService,
    TutorAccountSetupEmailService>();

builder.Services.AddScoped<
    ITutorEmailChangeEmailService,
    TutorEmailChangeEmailService>();

builder.Services.AddScoped<
    TutorEmailChangeService>();

builder.Services.AddScoped<
    IMessageProtectionService,
    MessageProtectionService>();

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

// Retry startup initialization only when Azure SQL is temporarily unavailable.
// Each DbInitializer call creates and disposes its own service scope.

const int maxInitializationAttempts = 3;

var initializationTimer = System.Diagnostics.Stopwatch.StartNew();

for (int attempt = 1; attempt <= maxInitializationAttempts; attempt++)
{
    try
    {
        var roleTimer = System.Diagnostics.Stopwatch.StartNew();

        await DbInitializer.SeedRolesAsync(
            app.Services,
            seedBasicAccounts: app.Environment.IsDevelopment());

        roleTimer.Stop();

        app.Logger.LogWarning(
            "PERFORMANCE: SeedRolesAsync took {ElapsedMs} ms.",
            roleTimer.ElapsedMilliseconds);

        var tutorTimer = System.Diagnostics.Stopwatch.StartNew();

        await DbInitializer.SeedEnrollmentTestTutorAsync(
            app.Services,
            app.Configuration);

        tutorTimer.Stop();

        app.Logger.LogWarning(
            "PERFORMANCE: SeedEnrollmentTestTutorAsync took {ElapsedMs} ms.",
            tutorTimer.ElapsedMilliseconds);

        initializationTimer.Stop();

        app.Logger.LogWarning(
            "PERFORMANCE: Total database initialization took {ElapsedMs} ms on attempt {Attempt}.",
            initializationTimer.ElapsedMilliseconds,
            attempt);

        app.Logger.LogInformation(
            "Database initialization completed on attempt {Attempt}.",
            attempt);

        break;
    }
    catch (Exception exception) when (
        attempt < maxInitializationAttempts &&
        IsDatabaseTemporarilyUnavailable(exception))
    {
        int delaySeconds = attempt * 5;

        app.Logger.LogWarning(
            "Azure SQL is temporarily unavailable during startup. " +
            "Attempt {Attempt}/{MaxAttempts}. Retrying in {DelaySeconds} seconds.",
            attempt,
            maxInitializationAttempts,
            delaySeconds);

        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
    }
    catch (Exception exception)
    {
        app.Logger.LogCritical(
            exception,
            "Database initialization failed. " +
            "Check database availability and application configuration.");

        throw;
    }
}

static bool IsDatabaseTemporarilyUnavailable(Exception exception)
{
    // EF Core can wrap the original SqlException.
    for (Exception? current = exception;
         current != null;
         current = current.InnerException)
    {
        if (current is not SqlException sqlException)
        {
            continue;
        }

        foreach (SqlError error in sqlException.Errors)
        {
            if (error.Number == 40613)
            {
                return true;
            }
        }
    }

    return false;
}

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
    string nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    context.Items["CspNonce"] = nonce;
    if (context.User.Identity?.IsAuthenticated == true)
        context.Response.Headers["Cache-Control"] = "no-store";
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
        $"script-src 'self' 'nonce-{nonce}'; " +
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
app.UseMiddleware<AuthenticatedSessionMiddleware>();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
        context.Response.Headers["Cache-Control"] = "no-store";
    await next();
});
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
