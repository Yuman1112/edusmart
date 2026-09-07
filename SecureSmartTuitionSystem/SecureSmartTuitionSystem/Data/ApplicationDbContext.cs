using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

        // Identity profiles
        public DbSet<ParentProfile> ParentProfiles =>
            Set<ParentProfile>();

        public DbSet<StudentProfile> StudentProfiles =>
            Set<StudentProfile>();

        // Class and Enrollment
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

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            ConfigureIdentityProfiles(builder);
            ConfigureSubjectsAndClasses(builder);
            ConfigureEnrollmentApplications(builder);
            ConfigureStudentEnrollments(builder);
            ConfigureEnrollmentStatusHistories(builder);
        }

        private static void ConfigureIdentityProfiles(
            ModelBuilder builder)
        {
            builder.Entity<ParentProfile>()
                .HasIndex(parent => parent.ApplicationUserId)
                .IsUnique();

            builder.Entity<ParentProfile>()
                .HasOne(parent => parent.ApplicationUser)
                .WithOne()
                .HasForeignKey<ParentProfile>(
                    parent => parent.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StudentProfile>()
                .HasIndex(student => student.StudentNumber)
                .IsUnique();

            builder.Entity<StudentProfile>()
                .HasIndex(student => student.ApplicationUserId)
                .IsUnique()
                .HasFilter("[ApplicationUserId] IS NOT NULL");

            builder.Entity<StudentProfile>()
                .HasOne(student => student.ParentProfile)
                .WithMany(parent => parent.Students)
                .HasForeignKey(student =>
                    student.ParentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StudentProfile>()
                .HasOne(student => student.ApplicationUser)
                .WithOne()
                .HasForeignKey<StudentProfile>(
                    student => student.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private static void ConfigureSubjectsAndClasses(
            ModelBuilder builder)
        {
            builder.Entity<Subject>()
                .Property(subject => subject.MonthlyFee)
                .HasPrecision(10, 2);

            builder.Entity<Subject>()
                .HasIndex(subject => new
                {
                    subject.SubjectName,
                    subject.AcademicLevel
                })
                .IsUnique();

            builder.Entity<ClassSession>()
                .HasIndex(session => session.ClassCode)
                .IsUnique();

            builder.Entity<ClassSession>()
                .HasOne(session => session.Subject)
                .WithMany(subject =>
                    subject.ClassSessions)
                .HasForeignKey(session =>
                    session.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ClassSession>()
                .HasOne(session => session.TutorUser)
                .WithMany()
                .HasForeignKey(session =>
                    session.TutorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private static void ConfigureEnrollmentApplications(
            ModelBuilder builder)
        {
            builder.Entity<EnrollmentApplication>()
                .HasIndex(application =>
                    application.ApplicationNumber)
                .IsUnique();

            builder.Entity<EnrollmentApplication>()
                .HasOne(application =>
                    application.SubmittedByUser)
                .WithMany()
                .HasForeignKey(application =>
                    application.SubmittedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EnrollmentApplication>()
                .HasOne(application =>
                    application.ReviewedByUser)
                .WithMany()
                .HasForeignKey(application =>
                    application.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EnrollmentApplication>()
                .HasOne(application =>
                    application.ExistingParentProfile)
                .WithMany()
                .HasForeignKey(application =>
                    application.ExistingParentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EnrollmentApplication>()
                .HasOne(application =>
                    application.ExistingStudentProfile)
                .WithMany()
                .HasForeignKey(application =>
                    application.ExistingStudentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EnrollmentApplicationSubject>()
                .Property(selected =>
                    selected.MonthlyFeeAtApplication)
                .HasPrecision(10, 2);

            builder.Entity<EnrollmentApplicationSubject>()
                .HasIndex(selected => new
                {
                    selected.EnrollmentApplicationId,
                    selected.ClassSessionId
                })
                .IsUnique();

            builder.Entity<EnrollmentApplicationSubject>()
                .HasOne(selected =>
                    selected.EnrollmentApplication)
                .WithMany(application =>
                    application.SelectedSubjects)
                .HasForeignKey(selected =>
                    selected.EnrollmentApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EnrollmentApplicationSubject>()
                .HasOne(selected =>
                    selected.ClassSession)
                .WithMany()
                .HasForeignKey(selected =>
                    selected.ClassSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private static void ConfigureStudentEnrollments(
            ModelBuilder builder)
        {
            builder.Entity<StudentEnrollment>()
                .Property(enrollment =>
                    enrollment.MonthlyFeeAtEnrollment)
                .HasPrecision(10, 2);

            // Speeds up enrollment checks and prevents a student
            // from having two active records for the same class.
            builder.Entity<StudentEnrollment>()
                .HasIndex(enrollment => new
                {
                    enrollment.StudentProfileId,
                    enrollment.ClassSessionId
                })
                .IsUnique()
                .HasFilter("[Status] IN (0, 1)");

            builder.Entity<StudentEnrollment>()
                .HasOne(enrollment =>
                    enrollment.StudentProfile)
                .WithMany()
                .HasForeignKey(enrollment =>
                    enrollment.StudentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StudentEnrollment>()
                .HasOne(enrollment =>
                    enrollment.ClassSession)
                .WithMany()
                .HasForeignKey(enrollment =>
                    enrollment.ClassSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StudentEnrollment>()
                .HasOne(enrollment =>
                    enrollment.EnrollmentApplication)
                .WithMany(application =>
                    application.StudentEnrollments)
                .HasForeignKey(enrollment =>
                    enrollment.EnrollmentApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private static void ConfigureEnrollmentStatusHistories(
            ModelBuilder builder)
        {
            builder.Entity<EnrollmentStatusHistory>()
                .HasIndex(history => new
                {
                    history.EnrollmentApplicationId,
                    history.ChangedAt
                });

            builder.Entity<EnrollmentStatusHistory>()
                .HasOne(history =>
                    history.EnrollmentApplication)
                .WithMany(application =>
                    application.StatusHistories)
                .HasForeignKey(history =>
                    history.EnrollmentApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EnrollmentStatusHistory>()
                .HasOne(history =>
                    history.ChangedByUser)
                .WithMany()
                .HasForeignKey(history =>
                    history.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}