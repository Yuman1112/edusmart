using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Services.Security;
using System.Security.Claims;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor _http;
        private readonly AuditLogIntegrity _auditIntegrity;
        private readonly FinancialFieldProtection _financialProtection;
        private readonly bool _auditChanges;
        private bool _auditPrepared;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor http, AuditLogIntegrity auditIntegrity,
            FinancialFieldProtection financialProtection, bool auditChanges = true) : base(options)
        {
            _http = http;
            _auditIntegrity = auditIntegrity;
            _financialProtection = financialProtection;
            _auditChanges = auditChanges;
        }

        public DbSet<EnrollmentExitRequest> EnrollmentExitRequests => Set<EnrollmentExitRequest>();
        public DbSet<ClassLessonChange> ClassLessonChanges => Set<ClassLessonChange>();
        public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
        public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
        public DbSet<SecurityEmailDelivery> SecurityEmailDeliveries => Set<SecurityEmailDelivery>();

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PrepareAuditEntries();
            int result = base.SaveChanges(acceptAllChangesOnSuccess);
            _auditPrepared = false;
            return result;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            PrepareAuditEntries();
            int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            _auditPrepared = false;
            return result;
        }

        private void PrepareAuditEntries()
        {
            ChangeTracker.DetectChanges();
            if (ChangeTracker.Entries<AuditLogEntry>().Any(x =>
                x.State is EntityState.Modified or EntityState.Deleted))
                throw new InvalidOperationException("Audit entries cannot be modified or deleted.");
            if (!_auditChanges || _auditPrepared) return;
            _auditPrepared = true;
            var context = _http.HttpContext;
            var changed = ChangeTracker.Entries().Where(x =>
                x.Entity is not AuditLogEntry and not SecurityEmailDelivery &&
                x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
            foreach (var change in changed)
            {
                string entity = change.Metadata.ClrType.Name;
                var keys = change.Properties.Where(x => x.Metadata.IsPrimaryKey() &&
                    !x.IsTemporary && x.Metadata.Name.EndsWith("Id", StringComparison.Ordinal))
                    .Select(x => $"{x.Metadata.Name}={x.CurrentValue}")
                    .Concat(change.Properties.Where(x =>
                        x.Metadata.Name is "PaymentNumber" or "BillNumber" or "ApplicationNumber" or "ClassCode")
                        .Select(x => $"{x.Metadata.Name}={x.CurrentValue}"));
                string fields = string.Join(", ", change.Properties.Where(x =>
                    change.State != EntityState.Modified || x.IsModified).Select(x => x.Metadata.Name));
                var entry = new AuditLogEntry {
                    ActorUserId = context?.User.FindFirstValue(ClaimTypes.NameIdentifier),
                    ActorName = SecurityEventService.Limit(context?.User.Identity?.Name ?? "System", 256),
                    EventName = SecurityEventService.Limit(entity + "." + change.State, 100),
                    Severity = SecuritySeverity.Information,
                    EntityName = SecurityEventService.Limit(entity, 120),
                    EntityKey = SecurityEventService.Limit(string.Join(", ", keys), 256),
                    RemoteIp = SecurityEventService.Limit(context?.Connection.RemoteIpAddress?.ToString() ?? "", 64),
                    TraceId = SecurityEventService.Limit(context?.TraceIdentifier ?? "", 120),
                    Summary = SecurityEventService.Limit($"{change.State} record. Fields: {fields}. Values are not logged.", 1000)
                };
                _auditIntegrity.Seal(entry);
                AuditLogEntries.Add(entry);
            }
        }

        /*
         * Identity Profiles
         */
        public DbSet<AdminProfile> AdminProfiles =>
            Set<AdminProfile>();

        public DbSet<TutorProfile> TutorProfiles =>
            Set<TutorProfile>();

        public DbSet<ParentProfile> ParentProfiles =>
            Set<ParentProfile>();

        public DbSet<StudentProfile> StudentProfiles =>
            Set<StudentProfile>();

        public DbSet<ParentStudent> ParentStudents =>
            Set<ParentStudent>();

        /*
         * Class and Enrollment
         */
        public DbSet<Subject> Subjects =>
            Set<Subject>();

        public DbSet<ClassSession> ClassSessions =>
            Set<ClassSession>();

        public DbSet<EnrollmentApplication>
            EnrollmentApplications =>
                Set<EnrollmentApplication>();

        public DbSet<EnrollmentApplicationSubject>
            EnrollmentApplicationSubjects =>
                Set<EnrollmentApplicationSubject>();

        public DbSet<StudentEnrollment> StudentEnrollments =>
            Set<StudentEnrollment>();

        public DbSet<EnrollmentStatusHistory>
            EnrollmentStatusHistories =>
                Set<EnrollmentStatusHistory>();

        /*
         * Payment and Billing
         */
        public DbSet<Bill> Bills =>
            Set<Bill>();

        public DbSet<BillItem> BillItems =>
            Set<BillItem>();

        public DbSet<Payment> Payments =>
            Set<Payment>();

        /*
         * Communication and Announcement
         */
        public DbSet<Announcement> Announcements =>
            Set<Announcement>();

        public DbSet<AnnouncementRead> AnnouncementReads =>
            Set<AnnouncementRead>();

        public DbSet<ConversationUserPreference> ConversationUserPreferences => Set<ConversationUserPreference>();

        public DbSet<Conversation> Conversations =>
            Set<Conversation>();

        public DbSet<ChatMessage> ChatMessages =>
            Set<ChatMessage>();

        public DbSet<MessageAttachment> MessageAttachments =>
            Set<MessageAttachment>();

        public DbSet<MessageUserDeletion> MessageUserDeletions =>
            Set<MessageUserDeletion>();

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // AddDefaultIdentity uses 128-character provider/token keys at runtime.
            // Apply the same mapping when the design-time factory builds the model.
            builder.Entity<IdentityUserLogin<string>>(entity =>
            {
                entity.Property(x => x.LoginProvider).HasMaxLength(128);
                entity.Property(x => x.ProviderKey).HasMaxLength(128);
            });
            builder.Entity<IdentityUserToken<string>>(entity =>
            {
                entity.Property(x => x.LoginProvider).HasMaxLength(128);
                entity.Property(x => x.Name).HasMaxLength(128);
            });

            ConfigureIdentityProfiles(builder);
            ConfigureSubjectsAndClasses(builder);
            ConfigureEnrollmentApplications(builder);
            ConfigureStudentEnrollments(builder);
            ConfigureEnrollmentStatusHistories(builder);
            ConfigureBilling(builder);
            ConfigureCommunication(builder);
            ConfigureSecurity(builder);
            builder.Entity<EnrollmentExitRequest>(entity =>
            {
                entity.HasIndex(x => x.StudentEnrollmentId).IsUnique();
                entity.HasIndex(x => x.ProviderRefundId).IsUnique().HasFilter("[ProviderRefundId] IS NOT NULL");
                entity.Property(x => x.EffectiveDate).HasColumnType("date");
                entity.HasOne(x => x.StudentEnrollment).WithMany().HasForeignKey(x => x.StudentEnrollmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<ClassLessonChange>(entity =>
            {
                entity.HasIndex(x => new { x.ClassSessionId, x.OriginalDate }).IsUnique();
                entity.Property(x => x.OriginalDate).HasColumnType("date");
                entity.Property(x => x.ReplacementDate).HasColumnType("date");
                entity.HasOne(x => x.ClassSession).WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<ClassSession>().Property(x => x.StartDate).HasColumnType("date");
            builder.Entity<ClassSession>().Property(x => x.EndDate).HasColumnType("date");
            builder.Entity<UserNotification>(entity =>
            {
                entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
                entity.Property(x => x.AvailableFromDate).HasColumnType("date");
                entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            });
        }

        private void ConfigureSecurity(ModelBuilder builder)
        {
            builder.Entity<AuditLogEntry>(entity =>
            {
                entity.ToTable("AuditLogEntries", table =>
                {
                    table.HasTrigger("TR_AuditLogEntries_AppendOnly");
                    table.UseSqlOutputClause(false);
                });
                entity.HasIndex(x => x.OccurredAtUtc);
                entity.HasIndex(x => new { x.Severity, x.OccurredAtUtc });
                entity.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc });
                entity.HasIndex(x => new { x.EventName, x.RemoteIp, x.OccurredAtUtc });
                entity.Property(x => x.IntegrityProof).IsRequired();
            });
            builder.Entity<SecurityEmailDelivery>(entity =>
            {
                entity.HasKey(x => x.AuditLogEntryId);
                entity.Property(x => x.AuditLogEntryId).ValueGeneratedNever();
                entity.HasOne<AuditLogEntry>().WithMany().HasForeignKey(x => x.AuditLogEntryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            var converter = new ValueConverter<string?, string?>(
                value => value == null ? null : _financialProtection.Encrypt(value),
                value => value == null ? null : _financialProtection.Decrypt(value));
            builder.Entity<EnrollmentExitRequest>().Property(x => x.ReviewNote).HasConversion(converter).HasColumnType("nvarchar(max)").Metadata.SetMaxLength(null);
            builder.Entity<Payment>().Property(x => x.FailureReason)
                .HasConversion(converter).HasColumnType("nvarchar(max)")
                .Metadata.SetMaxLength(null);
            builder.Entity<Bill>().Property(x => x.CancellationReason)
                .HasConversion(converter).HasColumnType("nvarchar(max)")
                .Metadata.SetMaxLength(null);
        }

        private static void ConfigureCommunication(
            ModelBuilder builder)
        {
            builder.Entity<Announcement>(entity =>
            {
                entity.HasIndex(item => item.PublishAt);
                entity.HasIndex(item => new
                {
                    item.TargetAudience,
                    item.IsArchived
                });

                entity.HasOne(item => item.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(item => item.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AnnouncementRead>(entity =>
            {
                entity.HasIndex(item => new
                {
                    item.AnnouncementId,
                    item.UserId
                })
                    .IsUnique();

                entity.HasIndex(item => new
                {
                    item.UserId,
                    item.ReadAt
                });

                entity.HasOne(item => item.Announcement)
                    .WithMany(item => item.Reads)
                    .HasForeignKey(item => item.AnnouncementId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Conversation>(entity =>
            {
                entity.HasIndex(item => item.ParentUserId).IsUnique();
                entity.HasIndex(item => item.LastMessageAt);

                entity.HasOne(item => item.ParentUser)
                    .WithMany()
                    .HasForeignKey(item => item.ParentUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ConversationUserPreference>(entity =>
            {
                entity.HasIndex(x => new { x.UserId, x.ConversationId }).IsUnique();
                entity.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                entity.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ChatMessage>(entity =>
            {
                entity.HasIndex(item => new { item.ConversationId, item.SentAt });
                entity.HasIndex(item => new { item.ConversationId, item.ReadAt });

                entity.HasOne(item => item.Conversation)
                    .WithMany(item => item.Messages)
                    .HasForeignKey(item => item.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.SenderUser)
                    .WithMany()
                    .HasForeignKey(item => item.SenderUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(item => item.DeletedAt);
            });

            builder.Entity<MessageAttachment>(entity =>
            {
                entity.HasIndex(item => item.ChatMessageId);
                entity.HasOne(item => item.ChatMessage)
                    .WithMany(item => item.Attachments)
                    .HasForeignKey(item => item.ChatMessageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<MessageUserDeletion>(entity =>
            {
                entity.HasIndex(item => new
                {
                    item.ChatMessageId,
                    item.UserId
                }).IsUnique();

                entity.HasOne(item => item.ChatMessage)
                    .WithMany(item => item.UserDeletions)
                    .HasForeignKey(item => item.ChatMessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureIdentityProfiles(
            ModelBuilder builder)
        {
            /*
             * Admin Profile
             */
            builder.Entity<AdminProfile>(entity =>
            {
                entity.HasIndex(admin =>
                        admin.ApplicationUserId)
                    .IsUnique();

                entity.HasOne(admin =>
                        admin.ApplicationUser)
                    .WithOne()
                    .HasForeignKey<AdminProfile>(
                        admin =>
                            admin.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            /*
             * Tutor Profile
             */
            builder.Entity<TutorProfile>(entity =>
            {
                entity.HasIndex(tutor =>
                        tutor.ApplicationUserId)
                    .IsUnique();

                entity.HasOne(tutor =>
                        tutor.ApplicationUser)
                    .WithOne()
                    .HasForeignKey<TutorProfile>(
                        tutor =>
                            tutor.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            /*
             * Parent Profile
             */
            builder.Entity<ParentProfile>(entity =>
            {
                entity.HasIndex(parent =>
                        parent.ApplicationUserId)
                    .IsUnique();

                entity.HasOne(parent =>
                        parent.ApplicationUser)
                    .WithOne()
                    .HasForeignKey<ParentProfile>(
                        parent =>
                            parent.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            /*
             * Student Profile
             */
            builder.Entity<StudentProfile>(entity =>
            {
                entity.HasIndex(student =>
                        student.StudentNumber)
                    .IsUnique();

                entity.HasIndex(student =>
                        student.IdentificationNumber)
                    .IsUnique();

                entity.HasIndex(student =>
                        student.ApplicationUserId)
                    .IsUnique()
                    .HasFilter(
                        "[ApplicationUserId] IS NOT NULL");

                entity.HasOne(student =>
                        student.ApplicationUser)
                    .WithOne()
                    .HasForeignKey<StudentProfile>(
                        student =>
                            student.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ParentStudent>(entity =>
            {
                entity.HasIndex(link => new
                {
                    link.ParentProfileId,
                    link.StudentProfileId
                })
                    .IsUnique();

                entity.HasOne(link =>
                        link.ParentProfile)
                    .WithMany(parent =>
                        parent.ParentStudents)
                    .HasForeignKey(link =>
                        link.ParentProfileId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(link =>
                        link.StudentProfile)
                    .WithMany(student =>
                        student.ParentStudents)
                    .HasForeignKey(link =>
                        link.StudentProfileId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureSubjectsAndClasses(
            ModelBuilder builder)
        {
            /*
             * Subject
             */
            builder.Entity<Subject>(entity =>
            {
                entity.Property(subject =>
                        subject.MonthlyFee)
                    .HasPrecision(10, 2);

                entity.HasIndex(subject => new
                {
                    subject.EducationStage,
                    subject.SubjectName,
                    subject.AcademicLevel
                })
                    .IsUnique();
            });

            /*
             * Class Session
             */
            builder.Entity<ClassSession>(entity =>
            {
                entity.HasIndex(session =>
                        session.ClassCode)
                    .IsUnique();

                entity.HasIndex(session =>
                        session.ClassName)
                    .IsUnique()
                    .HasFilter("[ClassName] IS NOT NULL");

                entity.HasOne(session =>
                        session.Subject)
                    .WithMany(subject =>
                        subject.ClassSessions)
                    .HasForeignKey(session =>
                        session.SubjectId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(session =>
                        session.TutorUser)
                    .WithMany()
                    .HasForeignKey(session =>
                        session.TutorUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureEnrollmentApplications(
            ModelBuilder builder)
        {
            builder.Entity<EnrollmentApplication>(entity =>
            {
                entity.HasIndex(application =>
                        application.ApplicationNumber)
                    .IsUnique();

                entity.Property(application =>
                        application.MonthlyTuitionFeeAtApplication)
                    .HasPrecision(10, 2);

                entity.Property(application =>
                        application.TuitionSubtotalAtApplication)
                    .HasPrecision(10, 2);

                entity.Property(application =>
                        application.DiscountRateAtApplication)
                    .HasPrecision(5, 2);

                entity.Property(application =>
                        application.DiscountAmountAtApplication)
                    .HasPrecision(10, 2);

                entity.Property(application =>
                        application.DiscountedTuitionAtApplication)
                    .HasPrecision(10, 2);

                entity.Property(application =>
                        application.HardcopyFeeAtApplication)
                    .HasPrecision(10, 2);

                entity.Property(application =>
                        application.ShippingFeeAtApplication)
                    .HasPrecision(10, 2);

                entity.Property(application =>
                        application.EstimatedTotalAtApplication)
                    .HasPrecision(10, 2);

                entity.HasOne(application =>
                        application.SubmittedByUser)
                    .WithMany()
                    .HasForeignKey(application =>
                        application.SubmittedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(application =>
                        application.ReviewedByUser)
                    .WithMany()
                    .HasForeignKey(application =>
                        application.ReviewedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(application =>
                        application.ExistingParentProfile)
                    .WithMany()
                    .HasForeignKey(application =>
                        application.ExistingParentProfileId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(application =>
                        application.ExistingStudentProfile)
                    .WithMany()
                    .HasForeignKey(application =>
                        application.ExistingStudentProfileId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<EnrollmentApplicationSubject>(
                entity =>
                {
                    entity.Property(selected =>
                            selected.MonthlyFeeAtApplication)
                        .HasPrecision(10, 2);

                    entity.HasIndex(selected => new
                    {
                        selected.EnrollmentApplicationId,
                        selected.ClassSessionId
                    })
                        .IsUnique();

                    entity.HasOne(selected =>
                            selected.EnrollmentApplication)
                        .WithMany(application =>
                            application.SelectedSubjects)
                        .HasForeignKey(selected =>
                            selected.EnrollmentApplicationId)
                        .OnDelete(DeleteBehavior.Cascade);

                    entity.HasOne(selected =>
                            selected.ClassSession)
                        .WithMany()
                        .HasForeignKey(selected =>
                            selected.ClassSessionId)
                        .OnDelete(DeleteBehavior.Restrict);
                });
        }

        private static void ConfigureStudentEnrollments(
            ModelBuilder builder)
        {
            builder.Entity<StudentEnrollment>(entity =>
            {
                entity.Property(enrollment =>
                        enrollment.MonthlyFeeAtEnrollment)
                    .HasPrecision(10, 2);

                /*
                 * Prevent duplicate current enrollment for
                 * the same student and class.
                 */
                entity.HasIndex(enrollment => new
                {
                    enrollment.StudentProfileId,
                    enrollment.ClassSessionId
                })
                    .IsUnique()
                    .HasFilter("[Status] IN (0, 1)");

                entity.HasOne(enrollment =>
                        enrollment.StudentProfile)
                    .WithMany()
                    .HasForeignKey(enrollment =>
                        enrollment.StudentProfileId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(enrollment =>
                        enrollment.ClassSession)
                    .WithMany()
                    .HasForeignKey(enrollment =>
                        enrollment.ClassSessionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(enrollment =>
                        enrollment.EnrollmentApplication)
                    .WithMany(application =>
                        application.StudentEnrollments)
                    .HasForeignKey(enrollment =>
                        enrollment.EnrollmentApplicationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureEnrollmentStatusHistories(
            ModelBuilder builder)
        {
            builder.Entity<EnrollmentStatusHistory>(
                entity =>
                {
                    entity.HasIndex(history => new
                    {
                        history.EnrollmentApplicationId,
                        history.ChangedAt
                    });

                    entity.HasOne(history =>
                            history.EnrollmentApplication)
                        .WithMany(application =>
                            application.StatusHistories)
                        .HasForeignKey(history =>
                            history.EnrollmentApplicationId)
                        .OnDelete(DeleteBehavior.Cascade);

                    entity.HasOne(history =>
                            history.ChangedByUser)
                        .WithMany()
                        .HasForeignKey(history =>
                            history.ChangedByUserId)
                        .OnDelete(DeleteBehavior.Restrict);
                });
        }

        private static void ConfigureBilling(
            ModelBuilder builder)
        {
            /*
             * Bill
             */
            builder.Entity<Bill>(entity =>
            {
                entity.HasIndex(bill =>
                        bill.BillNumber)
                    .IsUnique();

                entity.HasIndex(bill =>
                        bill.PaymentAccessTokenHash)
                    .IsUnique()
                    .HasFilter(
                        "[PaymentAccessTokenHash] IS NOT NULL");

                entity.HasIndex(bill =>
                        bill.EnrollmentApplicationId)
                    .IsUnique();

                entity.Property(bill =>
                        bill.TuitionSubtotal)
                    .HasPrecision(10, 2);

                entity.Property(bill =>
                        bill.TuitionDiscountRate)
                    .HasPrecision(5, 2);

                entity.Property(bill =>
                        bill.TuitionDiscountAmount)
                    .HasPrecision(10, 2);

                entity.Property(bill =>
                        bill.DiscountedTuitionAmount)
                    .HasPrecision(10, 2);

                entity.Property(bill =>
                        bill.HardcopyFee)
                    .HasPrecision(10, 2);

                entity.Property(bill =>
                        bill.DeliveryFee)
                    .HasPrecision(10, 2);

                entity.Property(bill =>
                        bill.TotalAmount)
                    .HasPrecision(10, 2);

                entity.HasOne(bill =>
                        bill.EnrollmentApplication)
                    .WithOne()
                    .HasForeignKey<Bill>(bill =>
                        bill.EnrollmentApplicationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            /*
             * Bill Item
             */
            builder.Entity<BillItem>(entity =>
            {
                entity.Property(item =>
                        item.UnitAmount)
                    .HasPrecision(10, 2);

                entity.Property(item =>
                        item.LineTotal)
                    .HasPrecision(10, 2);

                entity.HasOne(item =>
                        item.Bill)
                    .WithMany(bill =>
                        bill.Items)
                    .HasForeignKey(item =>
                        item.BillId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            /*
             * Payment
             */
            builder.Entity<Payment>(entity =>
            {
                entity.HasIndex(payment =>
                        payment.PaymentNumber)
                    .IsUnique();

                entity.HasIndex(payment =>
                        payment.ProviderCheckoutSessionId)
                    .IsUnique()
                    .HasFilter(
                        "[ProviderCheckoutSessionId] IS NOT NULL");

                entity.HasIndex(payment =>
                        payment.ProviderPaymentIntentId)
                    .IsUnique()
                    .HasFilter(
                        "[ProviderPaymentIntentId] IS NOT NULL");

                entity.HasIndex(payment =>
                        payment.ProviderEventId)
                    .IsUnique()
                    .HasFilter(
                        "[ProviderEventId] IS NOT NULL");

                entity.Property(payment =>
                        payment.Amount)
                    .HasPrecision(10, 2);

                entity.HasOne(payment =>
                        payment.Bill)
                    .WithMany(bill =>
                        bill.Payments)
                    .HasForeignKey(payment =>
                        payment.BillId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
