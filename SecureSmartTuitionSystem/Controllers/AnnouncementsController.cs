using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.ViewModels.Communication;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Admin,Parent,Student,Tutor")]
    public class AnnouncementsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnnouncementsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? searchTerm,
            AnnouncementAudience? audience,
            string? status)
        {
            bool isAdmin = User.IsInRole("Admin");
            DateTime now = DateTime.UtcNow;

            searchTerm = (searchTerm ?? string.Empty).Trim();
            if (searchTerm.Length > 100)
            {
                searchTerm = searchTerm[..100];
            }

            IQueryable<Announcement> query =
                _context.Announcements
                    .AsNoTracking()
                    .Include(item => item.CreatedByUser);

            if (!isAdmin)
            {
                AnnouncementAudience currentAudience =
                    GetCurrentAudience();

                query = query.Where(item =>
                    !item.IsArchived &&
                    item.PublishAt <= now &&
                    (!item.ExpiresAt.HasValue ||
                     item.ExpiresAt.Value > now) &&
                    (item.TargetAudience == AnnouncementAudience.All ||
                     item.TargetAudience == currentAudience));

                audience = null;
                status = null;
                searchTerm = string.Empty;
            }
            else
            {
                if (audience.HasValue)
                {
                    query = query.Where(item =>
                        item.TargetAudience == audience.Value);
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(item =>
                        item.Title.Contains(searchTerm) ||
                        item.Content.Contains(searchTerm));
                }

                status = NormalizeStatus(status);

                query = status switch
                {
                    "active" => query.Where(item =>
                        !item.IsArchived &&
                        item.PublishAt <= now &&
                        (!item.ExpiresAt.HasValue ||
                         item.ExpiresAt.Value > now)),

                    "scheduled" => query.Where(item =>
                        !item.IsArchived &&
                        item.PublishAt > now),

                    "expired" => query.Where(item =>
                        !item.IsArchived &&
                        item.ExpiresAt.HasValue &&
                        item.ExpiresAt.Value <= now),

                    "archived" => query.Where(item =>
                        item.IsArchived),

                    _ => query
                };
            }

            var records = await query
                .OrderByDescending(item => item.PublishAt)
                .ThenByDescending(item => item.CreatedAt)
                .ToListAsync();

            string? currentUserId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            HashSet<int> readAnnouncementIds = new();
            Dictionary<int, int> readCounts = new();

            if (!isAdmin &&
                !string.IsNullOrWhiteSpace(currentUserId))
            {
                readAnnouncementIds = (await _context.AnnouncementReads
                        .AsNoTracking()
                        .Where(item =>
                            item.UserId == currentUserId)
                        .Select(item => item.AnnouncementId)
                        .ToListAsync())
                    .ToHashSet();
            }
            else if (isAdmin && records.Count > 0)
            {
                var announcementIds = records
                    .Select(item => item.AnnouncementId)
                    .ToList();

                readCounts = await _context.AnnouncementReads
                    .AsNoTracking()
                    .Where(item =>
                        announcementIds.Contains(
                            item.AnnouncementId))
                    .GroupBy(item => item.AnnouncementId)
                    .Select(group => new
                    {
                        AnnouncementId = group.Key,
                        ReadCount = group.Count()
                    })
                    .ToDictionaryAsync(
                        item => item.AnnouncementId,
                        item => item.ReadCount);
            }

            var model = new AnnouncementIndexViewModel
            {
                IsAdmin = isAdmin,
                SearchTerm = searchTerm,
                Audience = audience,
                Status = status ?? string.Empty,
                Announcements = records
                    .Select(item => new AnnouncementListItemViewModel
                    {
                        AnnouncementId = item.AnnouncementId,
                        Title = item.Title,
                        ContentPreview = CreatePreview(item.Content),
                        TargetAudience = item.TargetAudience,
                        PublishAt = item.PublishAt,
                        ExpiresAt = item.ExpiresAt,
                        IsArchived = item.IsArchived,
                        CreatedBy = item.CreatedByUser.FullName ??
                            item.CreatedByUser.UserName ?? "Administrator",
                        DisplayStatus = GetDisplayStatus(item, now),
                        IsRead = isAdmin ||
                            readAnnouncementIds.Contains(
                                item.AnnouncementId),
                        ReadCount = readCounts.GetValueOrDefault(
                            item.AnnouncementId)
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            bool isAdmin = User.IsInRole("Admin");

            Announcement? announcement = await GetAccessibleQuery()
                .Include(item => item.CreatedByUser)
                .FirstOrDefaultAsync(item =>
                    item.AnnouncementId == id);

            if (announcement == null)
            {
                return NotFound();
            }

            int readCount = isAdmin
                ? await _context.AnnouncementReads
                    .AsNoTracking()
                    .CountAsync(item =>
                        item.AnnouncementId == id)
                : 0;

            var model = new AnnouncementDetailsViewModel
            {
                AnnouncementId = announcement.AnnouncementId,
                Title = announcement.Title,
                Content = announcement.Content,
                TargetAudience = announcement.TargetAudience,
                PublishAt = announcement.PublishAt,
                ExpiresAt = announcement.ExpiresAt,
                IsArchived = announcement.IsArchived,
                CreatedBy = announcement.CreatedByUser.FullName ??
                    announcement.CreatedByUser.UserName ??
                    "Administrator",
                DisplayStatus = GetDisplayStatus(
                    announcement,
                    DateTime.UtcNow),
                IsAdmin = isAdmin,
                ReadCount = readCount
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            if (User.IsInRole("Admin")) return NoContent();
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!await GetAccessibleQuery().AnyAsync(x => x.AnnouncementId == id)) return NotFound();
            if (!await _context.AnnouncementReads.AnyAsync(x => x.AnnouncementId == id && x.UserId == userId))
            {
                _context.AnnouncementReads.Add(new AnnouncementRead {
                    AnnouncementId = id, UserId = userId, ReadAt = DateTime.UtcNow
                });
                try { await _context.SaveChangesAsync(); }
                catch (DbUpdateException)
                {
                    if (!await _context.AnnouncementReads.AsNoTracking()
                        .AnyAsync(x => x.AnnouncementId == id && x.UserId == userId)) throw;
                }
            }
            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new AnnouncementFormViewModel
            {
                PublishAt = DateTime.Now,
                ExpiryUnit = AnnouncementExpiryUnit.None
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            AnnouncementFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var announcement = new Announcement
            {
                Title = model.Title.Trim(),
                Content = model.Content.Trim(),
                TargetAudience = model.TargetAudience,
                PublishAt = model.PublishAt.ToUniversalTime(),
                ExpiresAt = CalculateExpiry(model)?.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Announcement created successfully.";

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            Announcement? announcement =
                await _context.Announcements
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.AnnouncementId == id);

            if (announcement == null)
            {
                return NotFound();
            }

            DateTime localPublish = announcement.PublishAt.ToLocalTime();
            DateTime? localExpiry = announcement.ExpiresAt?.ToLocalTime();
            (AnnouncementExpiryUnit unit, int? duration) =
                GetExpirySelection(localPublish, localExpiry);

            return View(new AnnouncementFormViewModel
            {
                AnnouncementId = announcement.AnnouncementId,
                Title = announcement.Title,
                Content = announcement.Content,
                TargetAudience = announcement.TargetAudience,
                PublishAt = localPublish,
                ExpiryUnit = unit,
                ExpiryDuration = duration
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            AnnouncementFormViewModel model)
        {
            if (model.AnnouncementId != id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            Announcement? announcement =
                await _context.Announcements.FindAsync(id);

            if (announcement == null)
            {
                return NotFound();
            }

            announcement.Title = model.Title.Trim();
            announcement.Content = model.Content.Trim();
            announcement.TargetAudience = model.TargetAudience;
            announcement.PublishAt = model.PublishAt.ToUniversalTime();
            announcement.ExpiresAt = CalculateExpiry(model)?.ToUniversalTime();
            announcement.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Announcement updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            Announcement? announcement =
                await _context.Announcements.FindAsync(id);

            if (announcement == null)
            {
                return NotFound();
            }

            announcement.IsArchived = true;
            announcement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Announcement archived successfully.";

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            Announcement? announcement =
                await _context.Announcements.FindAsync(id);

            if (announcement == null)
            {
                return NotFound();
            }

            announcement.IsArchived = false;
            announcement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Announcement restored successfully.";

            return RedirectToAction(nameof(Index));
        }

        private IQueryable<Announcement> GetAccessibleQuery()
        {
            if (User.IsInRole("Admin"))
            {
                return _context.Announcements;
            }

            DateTime now = DateTime.UtcNow;
            AnnouncementAudience audience = GetCurrentAudience();

            return _context.Announcements.Where(item =>
                !item.IsArchived &&
                item.PublishAt <= now &&
                (!item.ExpiresAt.HasValue ||
                 item.ExpiresAt.Value > now) &&
                (item.TargetAudience == AnnouncementAudience.All ||
                 item.TargetAudience == audience));
        }

        private AnnouncementAudience GetCurrentAudience()
        {
            if (User.IsInRole("Parent"))
            {
                return AnnouncementAudience.Parent;
            }

            if (User.IsInRole("Student"))
            {
                return AnnouncementAudience.Student;
            }

            if (User.IsInRole("Tutor"))
            {
                return AnnouncementAudience.Tutor;
            }

            throw new InvalidOperationException(
                "The current role cannot access announcements.");
        }

        private static string GetDisplayStatus(
            Announcement announcement,
            DateTime now)
        {
            if (announcement.IsArchived)
            {
                return "Archived";
            }

            if (announcement.PublishAt > now)
            {
                return "Scheduled";
            }

            if (announcement.ExpiresAt.HasValue &&
                announcement.ExpiresAt.Value <= now)
            {
                return "Expired";
            }

            return "Active";
        }

        private static string NormalizeStatus(string? status)
        {
            string value = (status ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

            return value is "active" or "scheduled" or
                "expired" or "archived"
                    ? value
                    : string.Empty;
        }

        private static DateTime? CalculateExpiry(
            AnnouncementFormViewModel model)
        {
            if (model.ExpiryUnit == AnnouncementExpiryUnit.None)
            {
                return null;
            }

            int duration = model.ExpiryDuration ?? 0;

            DateTime expiryDate = model.ExpiryUnit switch
            {
                AnnouncementExpiryUnit.Days =>
                    model.PublishAt.Date.AddDays(duration),

                AnnouncementExpiryUnit.Weeks =>
                    model.PublishAt.Date.AddDays(duration * 7),

                AnnouncementExpiryUnit.Months =>
                    model.PublishAt.Date.AddMonths(duration),

                _ => throw new InvalidOperationException(
                    "The selected expiry option is invalid.")
            };

            return expiryDate.Date
                .AddHours(23)
                .AddMinutes(59)
                .AddSeconds(59);
        }

        private static (
            AnnouncementExpiryUnit Unit,
            int? Duration) GetExpirySelection(
                DateTime publishAt,
                DateTime? expiresAt)
        {
            if (!expiresAt.HasValue)
            {
                return (AnnouncementExpiryUnit.None, null);
            }

            DateTime expiryDate = expiresAt.Value.Date;

            for (int months = 1; months <= 12; months++)
            {
                if (publishAt.Date.AddMonths(months) == expiryDate)
                {
                    return (AnnouncementExpiryUnit.Months, months);
                }
            }

            int days = Math.Max(
                1,
                (expiryDate - publishAt.Date).Days);

            if (days % 7 == 0 && days / 7 <= 52)
            {
                return (AnnouncementExpiryUnit.Weeks, days / 7);
            }

            return (
                AnnouncementExpiryUnit.Days,
                Math.Min(days, 365));
        }

        private static string CreatePreview(string content)
        {
            const int maximumLength = 140;
            string value = content.Trim();

            return value.Length <= maximumLength
                ? value
                : value[..maximumLength] + "…";
        }
    }
}
