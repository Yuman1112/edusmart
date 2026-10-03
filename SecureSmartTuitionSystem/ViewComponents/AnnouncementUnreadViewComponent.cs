using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;

namespace SecureSmartTuitionSystem.ViewComponents
{
    public sealed class AnnouncementUnreadViewComponent
        : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public AnnouncementUnreadViewComponent(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (UserClaimsPrincipal.Identity?.IsAuthenticated != true ||
                UserClaimsPrincipal.IsInRole("Admin"))
            {
                return Content(string.Empty);
            }

            string? userId = UserClaimsPrincipal.FindFirstValue(
                ClaimTypes.NameIdentifier);

            AnnouncementAudience? audience = GetAudience();

            if (string.IsNullOrWhiteSpace(userId) ||
                !audience.HasValue)
            {
                return Content(string.Empty);
            }

            DateTime now = DateTime.UtcNow;

            int unreadCount = await _context.Announcements
                .AsNoTracking()
                .Where(item =>
                    !item.IsArchived &&
                    item.PublishAt <= now &&
                    (!item.ExpiresAt.HasValue ||
                     item.ExpiresAt.Value > now) &&
                    (item.TargetAudience == AnnouncementAudience.All ||
                     item.TargetAudience == audience.Value) &&
                    !item.Reads.Any(read => read.UserId == userId))
                .CountAsync();

            return View(unreadCount);
        }

        private AnnouncementAudience? GetAudience()
        {
            if (UserClaimsPrincipal.IsInRole("Parent"))
            {
                return AnnouncementAudience.Parent;
            }

            if (UserClaimsPrincipal.IsInRole("Student"))
            {
                return AnnouncementAudience.Student;
            }

            if (UserClaimsPrincipal.IsInRole("Tutor"))
            {
                return AnnouncementAudience.Tutor;
            }

            return null;
        }
    }
}
