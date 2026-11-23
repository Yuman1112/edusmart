using System.Text.Json;
using SecureSmartTuitionSystem.Models.Attendance;
using SecureSmartTuitionSystem.Services.Communication;
using SecureSmartTuitionSystem.ViewModels.Communication;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Models.Billing;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.Models.Learning;
using SecureSmartTuitionSystem.Models.Progress;

namespace SecureSmartTuitionSystem.Data;

public static class EduSmartDemoSeed
{
    public static async Task ImportAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = new SqlConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString);
        if (!app.Environment.IsDevelopment() ||
            !connection.DataSource.StartsWith(@"(localdb)\", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(connection.InitialCatalog, "EduSmartDemoDb", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(connection.AttachDBFilename))
        {
            throw new InvalidOperationException("Use Development with a separate SQL Server LocalDB database named EduSmartDemoDb.");
        }

        await db.Database.MigrateAsync();
        await DbInitializer.SeedRolesAsync(app.Services);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        const string password = "Password123!";
        var demoIds = Enumerable.Range(2, 8)
            .SelectMany(number => new[] { $"AD{number:D5}", $"TU{number:D5}", $"PA{number:D5}", $"ST{number:D5}" })
            .ToArray();
        var existing = await users.Users.Where(user => demoIds.Contains(user.UserName!)).ToListAsync();
        if (existing.Count != 0)
        {
            if (existing.Count != demoIds.Length || existing.Any(user =>
                !string.Equals(user.Email, $"{user.UserName!.ToLowerInvariant()}@example.invalid", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Demo IDs are occupied by unrecognized accounts. No demo data was changed.");
            }
            await BackfillAsync(db, existing, DateTime.UtcNow);
            await transaction.CommitAsync();
            Console.WriteLine("EduSmart demo import completed. Existing demo database updated.");
            return;
        }

        var protection = scope.ServiceProvider.GetRequiredService<IMessageProtectionService>();

        async Task<ApplicationUser> Account(string id, string name, string role)
        {
            var user = new ApplicationUser
            {
                UserName = id, FullName = name, Email = id.ToLowerInvariant() + "@example.invalid",
                EmailConfirmed = true, IsActive = true, LockoutEnabled = true,
                TwoFactorEnabled = false
            };
            Ensure(await users.CreateAsync(user, password));
            Ensure(await users.AddToRoleAsync(user, role));
            return user;
        }

        var now = DateTime.UtcNow;
        var today = now.AddHours(8).Date;
        var month = new DateTime(today.Year, today.Month, 1);
        var start = new DateTime(today.Year, 1, 1);
        var subjects = new[] { "Mathematics", "Science", "English", "Bahasa Melayu", "Geography", "History", "Chinese", "Computer Science" };
        for (var i = 0; i < subjects.Length; i++)
        {
            var number = 2 + i;
            var admin = await Account($"AD{number:D5}", AdminNames[i], "Admin");
            db.Add(new AdminProfile
            {
                ApplicationUserId = admin.Id, FullName = admin.FullName!,
                Position = "Administrator",
                PermissionLevel = AdminPermissionLevels.Standard
            });
            var tutor = await Account($"TU{number:D5}", TutorNames[i], "Tutor");
            var parent = await Account($"PA{number:D5}", ParentNames[i], "Parent");
            var learner = await Account($"ST{number:D5}", StudentNames[i], "Student");
            var fee = 60m + i * 5;
            var subject = new Subject { SubjectName = $"Demo {subjects[i]}", AcademicLevel = "FORM 1", EducationStage = EducationStage.Secondary, MonthlyFee = fee };
            var session = new ClassSession
            {
                ClassCode = $"CS{number:D5}", ClassName = $"FORM 1 - {subjects[i]} - {tutor.FullName}",
                Subject = subject, TutorUser = tutor, DayOfWeek = (DayOfWeek)((i + 1) % 7),
                StartTime = new TimeOnly(17 + i / 4, i % 2 * 30), EndTime = new TimeOnly(18 + i / 4, i % 2 * 30),
                Venue = $"Room {i + 1}", Capacity = 12, CreatedAt = now, StartDate = start,
                EndDate = start.AddYears(1).AddDays(-1), Status = ClassSessionStatus.Active
            };
            var guardian = new ParentProfile { ApplicationUserId = parent.Id, FullName = parent.FullName! };
            var student = new StudentProfile
            {
                ApplicationUserId = learner.Id, FullName = learner.FullName!, StudentNumber = learner.UserName!,
                IdentificationNumber = $"000000-00-{number:D4}", School = "Seri Wawasan Secondary School",
                Gender = i % 2 == 0 ? "Female" : "Male", AcademicLevel = "FORM 1",
                DateOfBirth = today.AddYears(-13), IsActive = true
            };
            db.AddRange(session, guardian, student, new TutorProfile
            {
                ApplicationUserId = tutor.Id, FullName = tutor.FullName!, Qualification = "Bachelor’s Degree",
                AreaOfExpertise = subjects[i], IsActive = true
            });
            await db.SaveChangesAsync();
            db.Add(new ParentStudent { ParentProfileId = guardian.ParentProfileId, StudentProfileId = student.StudentProfileId, Relationship = "Parent", IsPrimaryGuardian = true, IsActive = true });
            var enrolled = i < 6;
            var submitted = now.AddDays(-20 + i);
            var application = new EnrollmentApplication
            {
                ApplicationNumber = $"EA{number:D5}", SubmittedByUserId = parent.Id, ExistingParentProfileId = guardian.ParentProfileId,
                ExistingStudentProfileId = student.StudentProfileId, GuardianName = guardian.FullName, GuardianEmail = parent.Email!,
                GuardianPhoneNumber = "0000000000", StudentName = student.FullName, StudentIdentificationNumber = student.IdentificationNumber,
                StudentDateOfBirth = student.DateOfBirth, StudentGender = student.Gender, StudentSchool = student.School,
                StudentEmail = learner.Email, AcademicLevel = student.AcademicLevel, RequestedStartDate = enrolled ? start : today,
                EnrollmentDurationMonths = 1, MonthlyTuitionFeeAtApplication = fee, TuitionSubtotalAtApplication = fee, DiscountedTuitionAtApplication = fee, EstimatedTotalAtApplication = fee, SubmittedAt = submitted, ReviewedAt = i < 7 ? submitted.AddDays(1) : null,
                ReviewedByUserId = i < 7 ? admin.Id : null,
                Status = enrolled ? EnrollmentStatus.Enrolled : i == 6 ? EnrollmentStatus.ApprovedPendingPayment : EnrollmentStatus.PendingReview
            };
            db.Add(new EnrollmentApplicationSubject { EnrollmentApplication = application, ClassSessionId = session.ClassSessionId, MonthlyFeeAtApplication = fee });
            if (i < 7)
            {
                var bill = new Bill
                {
                    EnrollmentApplication = application, BillNumber = $"BL{number:D5}", TuitionSubtotal = fee,
                    DiscountedTuitionAmount = fee, TotalAmount = fee, IssuedAt = submitted.AddDays(1),
                    DueAt = enrolled ? submitted.AddDays(8) : now.AddDays(7), PaidAt = enrolled ? submitted.AddDays(2) : null,
                    Status = enrolled ? BillStatus.Paid : BillStatus.PendingPayment
                };
                db.Add(new BillItem { Bill = bill, ItemType = BillItemType.Tuition, Description = subjects[i], UnitAmount = fee, LineTotal = fee });
                if (enrolled)
                    db.Add(new Payment { Bill = bill, PaymentNumber = $"PY{number:D5}", Currency = "MYR", Amount = fee, PaymentProvider = "Demo", Status = PaymentStatus.Succeeded, CreatedAt = submitted.AddDays(2), CompletedAt = submitted.AddDays(2) });
            }

            var syllabus = new ProgressSyllabus { Subject = subject, AcademicYear = today.Year, TrackingFrom = start };
            var topic = new ProgressTopic { Syllabus = syllabus, Chapter = 1, Subchapter = 1, ChapterTitle = "Foundations", Title = "Core concepts" };
            var state = new ProgressClassState { ClassSession = session, Syllabus = syllabus };
            db.Add(new ProgressTopicState { ClassState = state, Topic = topic, Percent = 25 * (i % 4) });
            var announcement = new Announcement
            {
                Title = $"{subjects[i]} — Class update",
                Content = "Please review the class schedule before the next lesson.",
                TargetAudience = AnnouncementAudience.Parent, CreatedAt = now.AddDays(-i), PublishAt = now.AddDays(-i),
                CreatedByUserId = admin.Id
            };
            db.Add(new AnnouncementRead { Announcement = announcement, UserId = parent.Id, ReadAt = now });

            var conversation = new Conversation { ParentUserId = parent.Id, CreatedAt = now.AddDays(-1), LastMessageAt = now.AddHours(-1) };
            db.Add(new ChatMessage
            {
                Conversation = conversation, SenderUserId = parent.Id,
                ProtectedContent = protection.ProtectText($"Could you confirm the schedule for {subjects[i]}?"),
                SentAt = now.AddHours(-1)
            });
            db.Add(new GuestEnquiry
            {
                SubmissionKey = Guid.NewGuid(), CreatedAtUtc = now.AddDays(-i),
                ProtectedContact = protection.ProtectText(JsonSerializer.Serialize(
                    new GuestContact(ParentNames[(i + 3) % ParentNames.Length], $"demo{number}@example.invalid", $"Do you offer Form 1 {subjects[i]}?")))
            });
            db.Add(new EnrollmentStatusHistory
            {
                EnrollmentApplication = application, NewStatus = application.Status,
                ChangedAt = application.ReviewedAt ?? submitted,
                ChangedByUserId = application.ReviewedByUserId ?? parent.Id,
                Remarks = "Sample enrollment for demonstration."
            });
            db.Add(new UserNotification { UserId = parent.Id, Title = "Class schedule update", Text = $"Check the latest update for {subjects[i]}.", CreatedAtUtc = now });

            var nextLesson = today;
            while (nextLesson.DayOfWeek != session.DayOfWeek || nextLesson.Day > 28)
                nextLesson = nextLesson.AddDays(1);
            if (nextLesson <= session.EndDate.GetValueOrDefault())
            {
                db.Add(new ClassLessonChange
                {
                    ClassSession = session, OriginalDate = nextLesson, OriginalStartTime = session.StartTime,
                    OriginalEndTime = session.EndTime, CancellationReason = "Demo: tutor unavailable.",
                    ChangedByUserId = admin.Id, CancelledAtUtc = now
                });
            }

            if (!enrolled) continue;
            var enrollment = new StudentEnrollment
            {
                StudentProfile = student, ClassSession = session, EnrollmentApplication = application,
                StartDate = start, EnrolledAt = submitted.AddDays(2), MonthlyFeeAtEnrollment = fee, Status = StudentEnrollmentStatus.Active
            };
            db.Add(enrollment);
            await db.SaveChangesAsync();
            var lastLesson = today.AddDays(-1);
            while (lastLesson.DayOfWeek != session.DayOfWeek || lastLesson.Day > 28)
                lastLesson = lastLesson.AddDays(-1);
            if (lastLesson >= start)
            {
                var lesson = new AttendanceLesson
                {
                    ClassSession = session, OriginalDate = lastLesson, LessonDate = lastLesson,
                    StartTime = session.StartTime, EndTime = session.EndTime, TutorUserId = tutor.Id,
                    UpdatedByUserId = tutor.Id, ClassCode = session.ClassCode, ClassName = session.ClassName!,
                    TutorName = tutor.FullName!, SubmittedAtUtc = lastLesson.Add(session.EndTime.ToTimeSpan()).AddHours(-8)
                };
                db.Add(new AttendanceRecord
                {
                    AttendanceLesson = lesson, StudentProfile = student, StudentNumber = student.StudentNumber,
                    StudentName = student.FullName, Status = (AttendanceStatus)(1 + i % 3)
                });
                var progressLesson = new ProgressLessonRecord
                {
                    ClassState = state, LessonDate = lastLesson, OriginalDate = lastLesson,
                    Start = session.StartTime, End = session.EndTime, TutorUserId = tutor.Id,
                    SubmittedAtUtc = lesson.SubmittedAtUtc.Value, UpdatedAtUtc = lesson.SubmittedAtUtc.Value,
                    Notes = "Reviewed the previous lesson and practised core concepts."
                };
                db.Add(new ProgressChange
                {
                    LessonRecord = progressLesson, ClassSessionId = session.ClassSessionId,
                    Topic = topic, Before = 0, After = 25 * (i % 4), ActorUserId = tutor.Id,
                    RecordedAtUtc = progressLesson.SubmittedAtUtc, Note = progressLesson.Notes
                });
            }
            if (i != 0)
            {
                // Keep the first family available for the overdue billing demo.
                var effective = month.AddMonths(1);
                enrollment.Status = StudentEnrollmentStatus.PendingDrop;
                enrollment.DropRequestedAt = now;
                enrollment.DropEffectiveDate = effective;
                enrollment.WithdrawalReason = "Demo: class schedule conflict.";
                db.Add(new EnrollmentExitRequest
                {
                    StudentEnrollment = enrollment, RequestedByUserId = parent.Id, RequestedAtUtc = now,
                    EffectiveDate = effective, Reason = enrollment.WithdrawalReason, RefundStatus = ExitRefundStatus.NoRefundDue
                });
            }
        }
        await db.SaveChangesAsync();
        var createdAccounts = await users.Users.Where(user => demoIds.Contains(user.UserName!)).ToListAsync();
        await BackfillAsync(db, createdAccounts, now);
        await transaction.CommitAsync();
        Console.WriteLine("EduSmart demo import completed.");
    }

    private static readonly string[] StudentNames =
    [
        "Nur Alya Sofea", "Benjamin Lim Wei Jian", "Chloe Tan Jia En", "Devan Raj",
        "Ethan Wong Zhi Hao", "Nur Farah Imani", "Grace Lee Xin Yi", "Hana Aisyah"
    ];

    private static readonly string[] ParentNames =
    [
        "Nurul Huda Ahmad", "Lim Chee Keong", "Tan Mei Ling", "Ravi Kumar",
        "Wong Siew Ping", "Siti Mariam Hassan", "Lee Pei San", "Azlan Ismail"
    ];

    private static readonly string[] TutorNames =
    [
        "Adrian Goh", "Nadia Rahman", "Joanna Ng", "Aiman Hakim",
        "Kavitha Nair", "Gan Li Wen", "Yap Hui Min", "Daniel Teoh"
    ];

    private static readonly string[] AdminNames =
    [
        "Amira Yusof", "Marcus Chia", "Hannah Teh", "Pravin Menon",
        "Liyana Zaki", "Irene Chong", "Nabil Rahim", "Elaine Foo"
    ];

    private static async Task BackfillAsync(ApplicationDbContext db, List<ApplicationUser> accounts, DateTime now)
    {
        var byNumber = accounts.ToDictionary(user => user.UserName!, StringComparer.OrdinalIgnoreCase);
        var today = now.AddHours(8).Date;
        var january = new DateTime(today.Year, 1, 1);
        var current = new DateTime(today.Year, today.Month, 1);
        var learners = new List<StudentEnrollment>();

        for (int index = 0; index < StudentNames.Length; index++)
        {
            var suffix = (index + 2).ToString("D5");
            var student = byNumber[$"ST{suffix}"];
            var parent = byNumber[$"PA{suffix}"];
            var tutor = byNumber[$"TU{suffix}"];
            var admin = byNumber[$"AD{suffix}"];
            student.FullName = StudentNames[index];
            parent.FullName = ParentNames[index];
            tutor.FullName = TutorNames[index];
            admin.FullName = AdminNames[index];
            var studentProfile = await db.Set<StudentProfile>().SingleAsync(profile => profile.ApplicationUserId == student.Id);
            var parentProfile = await db.Set<ParentProfile>().SingleAsync(profile => profile.ApplicationUserId == parent.Id);
            var tutorProfile = await db.Set<TutorProfile>().SingleAsync(profile => profile.ApplicationUserId == tutor.Id);
            var adminProfile = await db.Set<AdminProfile>().SingleAsync(profile => profile.ApplicationUserId == admin.Id);
            studentProfile.FullName = student.FullName;
            parentProfile.FullName = parent.FullName;
            tutorProfile.FullName = tutor.FullName;
            adminProfile.FullName = admin.FullName;
            if (studentProfile.School == "Demo Secondary School") studentProfile.School = "Seri Wawasan Secondary School";

            var session = await db.Set<ClassSession>().Include(item => item.Subject)
                .SingleAsync(item => item.TutorUserId == tutor.Id && item.ClassCode == $"CS{suffix}");
            var subject = session.Subject;
            if (subject.SubjectName.StartsWith("Demo ", StringComparison.Ordinal))
            {
                var name = subject.SubjectName[5..];
                if (!await db.Set<Subject>().AnyAsync(other => other.SubjectId != subject.SubjectId &&
                    other.SubjectName == name && other.AcademicLevel == subject.AcademicLevel && other.EducationStage == subject.EducationStage))
                    subject.SubjectName = name;
            }

            var originalSubject = subject.SubjectName.StartsWith("Demo ", StringComparison.Ordinal) ? subject.SubjectName[5..] : subject.SubjectName;
            var previousClassName = $"{subject.AcademicLevel} - {originalSubject} - Tutor {originalSubject}";
            if (string.Equals(session.ClassName, previousClassName, StringComparison.Ordinal))
                session.ClassName = $"{subject.AcademicLevel} - {originalSubject} - {tutor.FullName}";
            if (session.Venue == $"Demo Room {index + 1}") session.Venue = $"Room {index + 1}";
            var applications = await db.Set<EnrollmentApplication>().Where(item =>
                item.SubmittedByUserId == parent.Id && item.ApplicationNumber == $"EA{suffix}").ToListAsync();
            foreach (var application in applications)
            {
                application.StudentName = student.FullName;
                application.GuardianName = parent.FullName;
                if (application.StudentSchool == "Demo Secondary School") application.StudentSchool = studentProfile.School;
            }
            var assessments = await db.Set<AssessmentParticipant>().Where(item =>
                item.StudentProfileId == studentProfile.StudentProfileId && item.Assessment.ClassSession.TutorUserId == tutor.Id).ToListAsync();
            foreach (var assessment in assessments) assessment.StudentName = student.FullName;
            var attendance = await db.Set<AttendanceRecord>().Include(item => item.AttendanceLesson).Where(item =>
                item.StudentProfileId == studentProfile.StudentProfileId && item.AttendanceLesson.ClassSessionId == session.ClassSessionId).ToListAsync();
            foreach (var record in attendance)
            {
                record.StudentName = student.FullName;
                record.AttendanceLesson.TutorName = tutor.FullName;
                record.AttendanceLesson.ClassName = session.ClassName ?? session.Subject.SubjectName;
            }
            var enrollment = await db.Set<StudentEnrollment>().Include(item => item.StudentProfile)
                .Include(item => item.ClassSession).ThenInclude(item => item.Subject)
                .SingleOrDefaultAsync(item => item.StudentProfileId == studentProfile.StudentProfileId && item.ClassSessionId == session.ClassSessionId);
            if (enrollment != null) learners.Add(enrollment);
        }

        if (learners.Count < 3) throw new InvalidOperationException("Three linked sample enrollments are required for monthly data.");
        var studentIds = learners.Select(item => item.StudentProfileId).ToArray();
        var classIds = learners.Select(item => item.ClassSessionId).ToArray();
        var invoices = await db.Set<MonthlyInvoice>().Where(item => studentIds.Contains(item.StudentProfileId) &&
            item.Month >= january && item.Month <= current).ToListAsync();
        var assignments = await db.Set<Assessment>().Where(item => classIds.Contains(item.ClassSessionId) &&
            item.Kind == AssessmentKind.Assignment && item.IsActive && item.PublishedAtUtc != null &&
            item.DueAtMyt >= january && item.DueAtMyt < current.AddMonths(1)).ToListAsync();

        var classStates = await db.Set<ProgressClassState>().Include(item => item.Syllabus)
            .Where(item => classIds.Contains(item.ClassSessionId) && item.Syllabus.AcademicYear == today.Year)
            .ToDictionaryAsync(item => item.ClassSessionId);
        var lessonRecords = await db.Set<ProgressLessonRecord>().Where(item => classIds.Contains(item.ClassSessionId) &&
            item.LessonDate >= january && item.LessonDate < current.AddMonths(1)).ToListAsync();
        var cancelledLessons = await db.Set<ClassLessonChange>().Where(item => classIds.Contains(item.ClassSessionId) &&
            item.OriginalDate >= january && item.OriginalDate < current.AddMonths(1)).ToListAsync();

        for (var period = january; period <= current; period = period.AddMonths(1))
        {
            var monthEnd = period.AddMonths(1);
            var billingCount = invoices.Count(item => item.Month == period);
            var assignmentCount = assignments.Count(item => item.DueAtMyt >= period && item.DueAtMyt < monthEnd);
            var lessonCount = lessonRecords.Count(item => item.LessonDate >= period && item.LessonDate < monthEnd);
            foreach (var enrollment in learners.Skip((period.Month - 1) % learners.Count).Concat(learners))
            {
                if (billingCount >= 3 && assignmentCount >= 3 && lessonCount >= 3) break;
                if (enrollment.StartDate >= monthEnd || (enrollment.DropEffectiveDate is DateTime end && end <= period) ||
                    (enrollment.EndedAt is DateTime ended && ended <= period)) continue;

                var session = enrollment.ClassSession;
                if (lessonCount < 3 && classStates.TryGetValue(session.ClassSessionId, out var state) &&
                    !lessonRecords.Any(item => item.ClassSessionId == session.ClassSessionId && item.LessonDate >= period && item.LessonDate < monthEnd))
                {
                    var date = period;
                    while (date < monthEnd && (date.DayOfWeek != session.DayOfWeek ||
                        cancelledLessons.Any(item => item.ClassSessionId == session.ClassSessionId && item.OriginalDate == date)))
                        date = date.AddDays(1);
                    var endedAt = date.Add(session.EndTime.ToTimeSpan()).AddHours(-8);
                    if (date < monthEnd && date.Day <= 28 && endedAt <= now && date >= state.Syllabus.TrackingFrom &&
                        (session.StartDate == null || date >= session.StartDate) &&
                        (session.EndDate == null || date <= session.EndDate))
                    {
                        var record = new ProgressLessonRecord
                        {
                            ClassSessionId = session.ClassSessionId, LessonDate = date, OriginalDate = date,
                            Start = session.StartTime, End = session.EndTime, TutorUserId = session.TutorUserId,
                            SubmittedAtUtc = endedAt, UpdatedAtUtc = endedAt, Notes = "Reviewed key concepts and completed practice questions."
                        };
                        db.Add(record);
                        lessonRecords.Add(record);
                        lessonCount++;
                    }
                }
                var day = period == current && today.Day < 8 ? today : period.AddDays(7);
                var fee = enrollment.MonthlyFeeAtEnrollment;
                if (billingCount < 3 && !invoices.Any(item => item.Month == period && item.StudentProfileId == enrollment.StudentProfileId))
                {
                    var invoice = new MonthlyInvoice
                    {
                        StudentProfileId = enrollment.StudentProfileId, Month = period, Total = fee,
                        IssuedAtUtc = period.AddHours(-8), DueAtUtc = period.AddDays(8).AddHours(-8),
                        Status = MonthlyInvoiceStatus.Paid, PaidAtUtc = day.AddHours(-8)
                    };
                    db.Add(new MonthlyInvoiceLine
                    {
                        MonthlyInvoice = invoice, StudentEnrollmentId = enrollment.StudentEnrollmentId,
                        Description = session.Subject.SubjectName, Amount = fee
                    });
                    invoices.Add(invoice);
                    billingCount++;
                }
                if (assignmentCount >= 3 || assignments.Any(item => item.ClassSessionId == session.ClassSessionId &&
                    item.DueAtMyt >= period && item.DueAtMyt < monthEnd)) continue;

                var deadline = day.AddHours(20);
                var isGraded = assignmentCount != 2 && deadline <= now.AddHours(8);
                var opened = day.AddDays(-3);
                var assignment = new Assessment
                {
                    ClassSessionId = session.ClassSessionId, Title = $"{session.Subject.SubjectName} Practice – {period:MMM yyyy}",
                    Kind = AssessmentKind.Assignment, OpensAtMyt = opened, DueAtMyt = deadline,
                    PublishedAtUtc = opened.AddHours(-8), ResultsPublishedAtUtc = isGraded ? deadline.AddHours(-8) : null,
                    CreatedByUserId = session.TutorUserId
                };
                db.Add(new AssessmentParticipant
                {
                    Assessment = assignment, StudentProfileId = enrollment.StudentProfileId,
                    StudentName = enrollment.StudentProfile.FullName, StudentNumber = enrollment.StudentProfile.StudentNumber,
                    SubmittedAtUtc = isGraded ? deadline.AddHours(-10) : null,
                    Marks = isGraded ? 70 + (period.Month + assignmentCount * 7) % 29 : null,
                    GradedAtUtc = isGraded ? deadline.AddHours(-8) : null,
                    GradedByUserId = isGraded ? session.TutorUserId : null
                });
                assignments.Add(assignment);
                assignmentCount++;
            }
        }
        await db.SaveChangesAsync();
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }
}
