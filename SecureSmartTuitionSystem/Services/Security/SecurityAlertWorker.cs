using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Settings;

namespace SecureSmartTuitionSystem.Services.Security;

public sealed class SecurityAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly EmailSettings _email;
    private readonly SecuritySettings _settings;
    private readonly ILogger<SecurityAlertWorker> _logger;
    public SecurityAlertWorker(IServiceScopeFactory scopes, IOptions<EmailSettings> email,
        IOptions<SecuritySettings> settings, ILogger<SecurityAlertWorker> logger)
    {
        _scopes = scopes; _email = email.Value; _settings = settings.Value; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(
            Math.Clamp(_settings.AlertIntervalMinutes, 1, 60)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await SendPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Security alert delivery failed; it will be retried."); }
        }
    }

    private async Task SendPendingAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.AdminAlertEmail) ||
            string.IsNullOrWhiteSpace(_email.Host) || string.IsNullOrWhiteSpace(_email.FromAddress)) return;
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var since = DateTime.UtcNow.AddDays(-7);
        var entries = await db.AuditLogEntries.AsNoTracking()
            .Where(x => (int)x.Severity >= (int)SecuritySeverity.High && x.OccurredAtUtc >= since &&
                !db.SecurityEmailDeliveries.Any(d => d.AuditLogEntryId == x.AuditLogEntryId))
            .OrderBy(x => x.OccurredAtUtc).Take(20).ToListAsync(cancellationToken);
        if (entries.Count == 0) return;
        using var message = new MailMessage {
            From = new MailAddress(_email.FromAddress, _email.FromName),
            Subject = "EduSmart security alert",
            Body = string.Join(Environment.NewLine, entries.Select(x =>
                $"{x.OccurredAtUtc:u} | {x.Severity} | {x.EventName} | Trace: {x.TraceId}")) +
                "\n\nSign in as Admin and open System & Security to review details.",
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(_settings.AdminAlertEmail));
        using var smtp = new SmtpClient(_email.Host, _email.Port) {
            EnableSsl = _email.UseSsl, UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_email.UserName, _email.Password), Timeout = 15000
        };
        await smtp.SendMailAsync(message, cancellationToken);
        db.SecurityEmailDeliveries.AddRange(entries.Select(x => new SecurityEmailDelivery {
            AuditLogEntryId = x.AuditLogEntryId, SentAtUtc = DateTime.UtcNow
        }));
        await db.SaveChangesAsync(cancellationToken);
    }
}
