using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Models.Tutor;

[Index(
    nameof(ApplicationUserId),
    nameof(Day),
    nameof(Slot),
    IsUnique = true)]
public sealed class TutorAvailabilitySlot
{
    public int TutorAvailabilitySlotId { get; set; }

    [Required]
    [MaxLength(450)]
    public string ApplicationUserId { get; set; } = string.Empty;

    public ApplicationUser ApplicationUser { get; set; } = null!;

    // Sunday = 0, Monday = 1, ..., Saturday = 6.
    [Range(0, 6)]
    public int Day { get; set; }

    // Each slot represents 30 minutes.
    // 0 = 00:00–00:30, 47 = 23:30–24:00.
    [Range(0, 47)]
    public int Slot { get; set; }
}