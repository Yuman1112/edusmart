using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        /*
         * Identity profiles
         */
        public DbSet<ParentProfile> ParentProfiles =>
            Set<ParentProfile>();

        public DbSet<StudentProfile> StudentProfiles =>
            Set<StudentProfile>();

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

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            ConfigureIdentityProfiles(builder);
            ConfigureSubjectsAndClasses(builder);
            ConfigureEnrollmentApplications(builder);
            ConfigureStudentEnrollments(builder);
            ConfigureEnrollmentStatusHistories(builder);
            ConfigureBilling(builder);
        }

        private static void ConfigureIdentityProfiles(
            ModelBuilder builder)
        {
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

            builder.Entity<StudentProfile>(entity =>
            {
                entity.HasIndex(student =>
                        student.StudentNumber)
                    .IsUnique();

                entity.HasIndex(student =>
                        student.ApplicationUserId)
                    .IsUnique()
                    .HasFilter(
                        "[ApplicationUserId] IS NOT NULL");

                entity.HasOne(student =>
                        student.ParentProfile)
                    .WithMany(parent =>
                        parent.Students)
                    .HasForeignKey(student =>
                        student.ParentProfileId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(student =>
                        student.ApplicationUser)
                    .WithOne()
                    .HasForeignKey<StudentProfile>(
                        student =>
                            student.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureSubjectsAndClasses(
            ModelBuilder builder)
        {
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

            builder.Entity<ClassSession>(entity =>
            {
                entity.HasIndex(session =>
                        session.ClassCode)
                    .IsUnique();

                entity.HasIndex(session =>
                        session.ClassName)
                    .IsUnique()
                    .HasFilter(
                        "[ClassName] IS NOT NULL");

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
            builder.Entity<Bill>(entity =>
            {
                entity.HasIndex(bill =>
                        bill.BillNumber)
                    .IsUnique();

                /*
                 * One enrollment application can only
                 * generate one bill.
                 */
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

            builder.Entity<Payment>(entity =>
            {
                entity.HasIndex(payment =>
                        payment.PaymentNumber)
                    .IsUnique();

                /*
                 * Stripe identifiers are unique.
                 * The filters allow multiple null values.
                 */
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