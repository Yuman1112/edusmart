namespace SecureSmartTuitionSystem.Models.Enrollment
{
    public class EnrollmentApplicationSubject
    {
        public int EnrollmentApplicationSubjectId { get; set; }

        public int EnrollmentApplicationId { get; set; }

        public int ClassSessionId { get; set; }

        // The fee is copied at application time so later fee changes
        // do not alter the original application amount.
        public decimal MonthlyFeeAtApplication { get; set; }

        public EnrollmentApplication EnrollmentApplication { get; set; }
            = null!;

        public ClassSession ClassSession { get; set; } = null!;
    }
}