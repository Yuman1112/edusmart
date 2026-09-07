using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Services;

namespace SecureSmartTuitionSystem.Data;

/// <summary>
/// EF Core Code-First DbContext for the five modules covered by this report.
/// Every LINQ query issued through this context is automatically
/// parameterised by EF Core (Ch.2.5 "Parameterised Queries" / Ch.2.4
/// "Database Integration"), so no raw SQL string concatenation appears
/// anywhere in the Controllers or Services layers.
/// </summary>
public class AppDbContext : DbContext
{
    private readonly IEncryptionService _encryption;

    public AppDbContext(DbContextOptions<AppDbContext> options, IEncryptionService encryption)
        : base(options)
    {
        _encryption = encryption;
    }

    // Class & Enrollment Management
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Tutor> Tutors => Set<Tutor>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<EnrollmentApplicant> EnrollmentApplicants => Set<EnrollmentApplicant>();
    public DbSet<EnrollmentSubject> EnrollmentSubjects => Set<EnrollmentSubject>();
    public DbSet<SubjectChangeRequest> SubjectChangeRequests => Set<SubjectChangeRequest>();

    // Payment & Billing
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<PaymentLink> PaymentLinks => Set<PaymentLink>();

    // Communication & Announcement
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<ChatbotFaq> ChatbotFaqs => Set<ChatbotFaq>();
    public DbSet<ChatbotKeyword> ChatbotKeywords => Set<ChatbotKeyword>();
    public DbSet<ChatbotLog> ChatbotLogs => Set<ChatbotLog>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementRecipient> AnnouncementRecipients => Set<AnnouncementRecipient>();

    // Reporting & Analytics reads Enrollment/Invoice/Payment/Assignment directly (no dedicated tables of its own)
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();

    // System Security
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- AES-256 value converters -----------------------------------------------------
        // Applied automatically on every SaveChanges/query for the specific
        // Payment & Billing columns identified in Ch.2.4 as needing
        // encryption "before these values are persisted by Entity Framework
        // Core into the SQL Server database".
        var aesConverter = new ValueConverter<string, string>(
            plain => _encryption.Encrypt(plain),
            cipher => _encryption.Decrypt(cipher));

        var aesConverterNullable = new ValueConverter<string?, string?>(
            plain => plain == null ? null : _encryption.Encrypt(plain),
            cipher => cipher == null ? null : _encryption.Decrypt(cipher));

        modelBuilder.Entity<Payment>()
            .Property(p => p.GatewayTokenEncrypted)
            .HasConversion(aesConverter)
            .HasMaxLength(500);

        modelBuilder.Entity<Payment>()
            .Property(p => p.PayerNoteEncrypted)
            .HasConversion(aesConverterNullable)
            .HasMaxLength(500);

        modelBuilder.Entity<Invoice>()
            .Property(i => i.RemarksEncrypted)
            .HasConversion(aesConverterNullable)
            .HasMaxLength(500);

        // --- Guardian / Student -------------------------------------------------------------
        modelBuilder.Entity<Guardian>()
            .HasIndex(g => g.Email)
            .IsUnique(); // supports the O(1) exact-match duplicate check (Ch.2.2)

        modelBuilder.Entity<Student>()
            .HasIndex(s => s.BirthDate); // blocking index for the Jaro-Winkler filter-then-verify pattern (Ch.2.2)

        modelBuilder.Entity<Student>()
            .HasOne(s => s.Guardian)
            .WithMany(g => g.Students)
            .HasForeignKey(s => s.GuardianId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Enrollment ----------------------------------------------------------------------
        modelBuilder.Entity<Enrollment>()
            .HasIndex(e => e.Status);

        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.ClassSession)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.ClassSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EnrollmentApplicant>()
            .HasOne(a => a.Enrollment)
            .WithOne(e => e.Applicant)
            .HasForeignKey<EnrollmentApplicant>(a => a.EnrollmentId);

        modelBuilder.Entity<EnrollmentSubject>()
            .HasIndex(es => new { es.EnrollmentId, es.SubjectId })
            .IsUnique();

        // --- Payment & Billing -----------------------------------------------------------
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.StudentId, i.Status });

        modelBuilder.Entity<Receipt>()
            .HasIndex(r => r.ReceiptNumber)
            .IsUnique();

        modelBuilder.Entity<PaymentLink>()
            .HasIndex(pl => pl.Token)
            .IsUnique();

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Receipt)
            .WithOne(r => r.Payment)
            .HasForeignKey<Receipt>(r => r.PaymentId);

        // --- Communication & Announcement -------------------------------------------------
        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.NotificationType, n.CreatedAt });
            // supports the "once per notification-type per user per day" throttle (NotificationService)

        modelBuilder.Entity<ConversationParticipant>()
            .HasIndex(cp => new { cp.ConversationId, cp.UserId })
            .IsUnique();

        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ConversationId, m.SentAt });

        modelBuilder.Entity<ChatbotKeyword>()
            .HasIndex(k => k.Keyword);

        modelBuilder.Entity<AnnouncementRecipient>()
            .HasIndex(ar => new { ar.AnnouncementId, ar.UserId })
            .IsUnique();

        // --- System Security ----------------------------------------------------------------
        modelBuilder.Entity<ActivityLog>()
            .HasIndex(a => a.OccurredAt);

        // --- Decimal precision guardrails (SQL Server default would truncate silently) ------
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            if (property.GetColumnType() is null)
            {
                property.SetColumnType("decimal(10,2)");
            }
        }

        // --- Enums stored as their string name, not the default int --------------------------
        // Schema.sql declares every status/type column as NVARCHAR (e.g. Payments.Status,
        // Notifications.NotificationType) so it reads clearly outside the app, and
        // ReportingService's raw ROLLUP query compares against string literals like
        // 'Success' - both would silently break against EF Core's default int-backed enum
        // storage, so every (non-nullable) enum property is switched to a string conversion.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (!property.ClrType.IsEnum) continue;

                var converterType = typeof(EnumToStringConverter<>).MakeGenericType(property.ClrType);
                var converter = (ValueConverter)Activator.CreateInstance(converterType, (ConverterMappingHints?)null)!;
                property.SetValueConverter(converter);
                property.SetMaxLength(40);
            }
        }
    }
}
