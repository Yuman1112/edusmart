using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureSmartTuitionSystem.Models;

/// <summary>[STUB - User Authentication &amp; RBAC Module] Minimal identity row this report's modules key off.</summary>
public class ApplicationUser
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(256)]
    public string UserName { get; set; } = null!;

    [Required, MaxLength(256), EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public UserRole Role { get; set; }

    /// <summary>Flipped to false by the Payment &amp; Billing overdue-suspension rule.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>[STUB - Tutor Management Module]</summary>
public class Tutor
{
    public int TutorId { get; set; }

    [Required]
    public string UserId { get; set; } = null!;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(150)]
    public string FullName { get; set; } = null!;

    [MaxLength(100)]
    public string? SubjectSpecialism { get; set; }
}

public class Guardian
{
    public int GuardianId { get; set; }

    /// <summary>Null until the enrollment fee is confirmed paid (report: "account only activated after payment").</summary>
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(150)]
    public string FullName { get; set; } = null!;

    [Required, MaxLength(256), EmailAddress]
    public string Email { get; set; } = null!;

    [Required, MaxLength(30), Phone]
    public string PhoneNumber { get; set; } = null!;

    [Required, MaxLength(30)]
    public string IdentificationNo { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Student> Students { get; set; } = new List<Student>();

    /// <summary>Last four digits used for the exact-match verification rule (Ch.2.2 Adopted Approach).</summary>
    [NotMapped]
    public string PhoneOrIcLast4 =>
        (IdentificationNo ?? PhoneNumber).Length >= 4
            ? (IdentificationNo ?? PhoneNumber)[^4..]
            : (IdentificationNo ?? PhoneNumber);
}

public class Student
{
    public int StudentId { get; set; }

    /// <summary>Null until the enrollment fee is confirmed paid.</summary>
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required]
    public int GuardianId { get; set; }
    public Guardian Guardian { get; set; } = null!;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = null!;

    [Required]
    [DataType(DataType.Date)]
    public DateOnly BirthDate { get; set; }

    [Required, MaxLength(20)]
    public string FormYear { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Subject
{
    public int SubjectId { get; set; }

    [Required, MaxLength(100)]
    public string SubjectName { get; set; } = null!;

    [Required, MaxLength(20)]
    public string FormYear { get; set; } = null!;

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal MonthlyFee { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ClassSession
{
    public int ClassSessionId { get; set; }

    [Required]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public int? TutorId { get; set; }
    public Tutor? Tutor { get; set; }

    [Range(0, 6)]
    public byte DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    [Required, MaxLength(100)]
    public string Venue { get; set; } = null!;

    public int MinCapacity { get; set; } = 1;
    public int MaxCapacity { get; set; } = 30;

    public ClassSessionStatus Status { get; set; } = ClassSessionStatus.Active;

    [MaxLength(300)]
    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    /// <summary>Seats already taken by an Approved or Pending enrollment (Check Class Capacity, Table 4.4).</summary>
    [NotMapped]
    public int SeatsTaken => Enrollments.Count(e =>
        e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Pending);
}

/// <summary>
/// One row per class a student/guardian applies to join. This is the anchor
/// record for the whole "Submit Enrollment Request -&gt; duplicate check -&gt;
/// Approve/Reject -&gt; fee calculation -&gt; payment" pipeline described across
/// Chapter 3 and Chapter 4.
/// </summary>
public class Enrollment
{
    public int EnrollmentId { get; set; }

    /// <summary>Set once the duplicate-check/administrator review confirms which Student row this maps to.</summary>
    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    public int? GuardianId { get; set; }
    public Guardian? Guardian { get; set; }

    [Required]
    public int ClassSessionId { get; set; }
    public ClassSession ClassSession { get; set; } = null!;

    public DateOnly RequestedStartDate { get; set; }

    public bool HardcopyMaterialsOptIn { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;

    public bool DuplicateFlag { get; set; }
    public int? DuplicateMatchStudentId { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? DuplicateMatchScore { get; set; }

    [MaxLength(300)]
    public string? RejectionReason { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public string? DecidedByUserId { get; set; }

    public EnrollmentApplicant? Applicant { get; set; }
    public ICollection<EnrollmentSubject> EnrollmentSubjects { get; set; } = new List<EnrollmentSubject>();
}

/// <summary>
/// The raw, as-typed applicant details captured at submission time, kept
/// separate from Student/Guardian because — per the report — no Student or
/// Guardian record may be treated as "real" until the duplicate check and
/// (if flagged) administrator review have both cleared it.
/// </summary>
public class EnrollmentApplicant
{
    [Key, ForeignKey(nameof(Enrollment))]
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    [Required, MaxLength(150)]
    public string StudentFullName { get; set; } = null!;

    public DateOnly StudentBirthDate { get; set; }

    [Required, MaxLength(150)]
    public string GuardianFullName { get; set; } = null!;

    [Required, MaxLength(256), EmailAddress]
    public string GuardianEmail { get; set; } = null!;

    /// <summary>Full number, captured so a real Guardian record can be created on approval; only the last 4 digits are ever used for the exact-match duplicate check (Ch.2.2).</summary>
    [Required, MaxLength(30)]
    public string GuardianPhoneNumber { get; set; } = null!;

    [Required, MaxLength(30)]
    public string GuardianIdentificationNo { get; set; } = null!;

    [NotMapped]
    public string GuardianPhoneOrIcLast4 =>
        GuardianIdentificationNo.Length >= 4 ? GuardianIdentificationNo[^4..] : GuardianIdentificationNo;
}

public class EnrollmentSubject
{
    public int EnrollmentSubjectId { get; set; }

    [Required]
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    [Required]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}

/// <summary>Add/Drop Subject Billing Adjustment (Table 4.12).</summary>
public class SubjectChangeRequest
{
    public int SubjectChangeRequestId { get; set; }

    [Required]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Required]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public SubjectChangeType ChangeType { get; set; }

    /// <summary>First day of the billing month this change applies to.</summary>
    public DateOnly EffectiveMonth { get; set; }

    [Required]
    public string RequestedByUserId { get; set; } = null!;

    public SubjectChangeStatus Status { get; set; } = SubjectChangeStatus.Pending;

    /// <summary>Double confirmation required only for a start date within the final two weeks of a month; reused here for consistency.</summary>
    public bool DoubleConfirmed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
