using System.Data;
using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Security;
using SecureSmartTuitionSystem.Models.Security;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.Services.Communication;
using SecureSmartTuitionSystem.ViewModels.Communication;

namespace SecureSmartTuitionSystem.Controllers
{
    [Authorize(Roles = "Admin,Parent,Student,Tutor")]
    public sealed class MessagesController : Controller
    {
        private const long MaximumFileSize = 10 * 1024 * 1024;
        private static readonly TimeSpan MessageChangeWindow =
            TimeSpan.FromMinutes(15);

        private static readonly Dictionary<string, string[]> AllowedFiles =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [".pdf"] = new[] { "application/pdf" },
                [".jpg"] = new[] { "image/jpeg" },
                [".jpeg"] = new[] { "image/jpeg" },
                [".png"] = new[] { "image/png" },
                [".docx"] = new[]
                {
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                }
            };

        private readonly ApplicationDbContext _context;
        private readonly SecurityEventService _events;
        private readonly ChatbotSessionService _chatbotSessions;
        private readonly IMessageProtectionService _protection;
        private readonly IWebHostEnvironment _environment;

        public MessagesController(
            ApplicationDbContext context,
            IMessageProtectionService protection,
            IWebHostEnvironment environment, SecurityEventService events, ChatbotSessionService chatbotSessions)
        {
            _context = context;
            _protection = protection;
            _environment = environment;
            _events = events;
            _chatbotSessions = chatbotSessions;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? id)
        {
            string userId = CurrentUserId();
            bool isAdmin = User.IsInRole("Admin");

            HashSet<int> hiddenMessageIds = (await _context.MessageUserDeletions
                    .AsNoTracking()
                    .Where(item => item.UserId == userId)
                    .Select(item => item.ChatMessageId)
                    .ToListAsync())
                .ToHashSet();

            IQueryable<Conversation> accessible = AccessibleConversations(userId, isAdmin);

            if (id.HasValue &&
                !await accessible.AnyAsync(item => item.ConversationId == id.Value))
            {
                return NotFound();
            }

            var preferences = await _context.ConversationUserPreferences.Where(x => x.UserId == userId)
                .ToDictionaryAsync(x => x.ConversationId);
            if (id.HasValue && preferences.TryGetValue(id.Value, out var opened) && opened.IsHidden)
            {
                opened.IsHidden = false;
                await _context.SaveChangesAsync();
            }

            var conversations = await accessible
                .AsNoTracking()
                .Include(item => item.ParentUser)
                .Include(item => item.Messages)
                    .ThenInclude(message => message.Attachments)
                .OrderByDescending(item => item.LastMessageAt ?? item.CreatedAt)
                .ToListAsync();

            conversations = conversations.Where(c => !preferences.TryGetValue(c.ConversationId, out var preference) ||
                    !preference.IsHidden || c.Messages.Any(m => !m.IsDeletedForEveryone && !hiddenMessageIds.Contains(m.ChatMessageId) &&
                        (!preference.HiddenAtUtc.HasValue || m.SentAt > preference.HiddenAtUtc.Value)))
                .OrderByDescending(c => preferences.TryGetValue(c.ConversationId, out var preference) && preference.IsPinned)
                .ThenByDescending(c => c.LastMessageAt ?? c.CreatedAt).ToList();

            Conversation? active = id.HasValue
                ? conversations.FirstOrDefault(item => item.ConversationId == id.Value)
                : null;

            var model = new MessageIndexViewModel
            {
                IsAdmin = isAdmin,
                ActiveConversationId = active?.ConversationId,
                ActiveParticipantName = isAdmin
                    ? active?.ParentUser.FullName ?? active?.ParentUser.UserName ?? string.Empty
                    : "EduSmart Administration",
                ActiveParticipantEmail = isAdmin
                    ? active?.ParentUser.UserName ?? string.Empty
                    : "Official support conversation",
                Conversations = conversations.Select(item =>
                {
                    ChatMessage? last = item.Messages
                        .Where(message =>
                            !hiddenMessageIds.Contains(message.ChatMessageId))
                        .OrderByDescending(message => message.SentAt)
                        .FirstOrDefault();

                    return new ConversationListItemViewModel
                    {
                        ConversationId = item.ConversationId,
                        IsPinned = preferences.TryGetValue(item.ConversationId, out var preference) && preference.IsPinned,
                        ParticipantUserId = item.ParentUserId,
                        ParticipantName = isAdmin
                            ? item.ParentUser.FullName ?? item.ParentUser.UserName ?? "Parent"
                            : "EduSmart Administration",
                        ParticipantEmail = isAdmin
                            ? item.ParentUser.UserName ?? string.Empty
                            : "Official support conversation",
                        LastMessagePreview = last == null
                            ? "No messages yet"
                            : last.IsDeletedForEveryone
                                ? "This message was deleted"
                            : Preview(_protection.UnprotectText(last.ProtectedContent),
                                last.Attachments.Count > 0),
                        LastMessageAt = last?.SentAt,
                        UnreadCount = item.Messages.Count(message =>
                            (isAdmin ? message.SenderUserId == item.ParentUserId : message.SenderUserId != userId) &&
                            !message.IsDeletedForEveryone &&
                            !hiddenMessageIds.Contains(message.ChatMessageId) &&
                            !message.ReadAt.HasValue &&
                            (!preferences.TryGetValue(item.ConversationId, out var hiddenPreference) || !hiddenPreference.IsHidden ||
                             !hiddenPreference.HiddenAtUtc.HasValue || message.SentAt > hiddenPreference.HiddenAtUtc.Value))
                    };
                }).ToList()
            };

            if (isAdmin)
            {
                var contacts = await (from user in _context.Users.AsNoTracking()
                    join membership in _context.UserRoles on user.Id equals membership.UserId
                    join role in _context.Roles on membership.RoleId equals role.Id
                    where user.IsActive && (role.Name == "Parent" || role.Name == "Student" || role.Name == "Tutor")
                    select new { user.Id, user.FullName, user.UserName, user.Email, Role = role.Name }).ToListAsync();
                model.AvailableParents = contacts.GroupBy(x => x.Id).Select(group =>
                {
                    var contact = group.OrderBy(x => x.Role == "Parent" ? 0 : x.Role == "Student" ? 1 : 2).First();
                    return new ParentContactViewModel { UserId = contact.Id, Name = contact.FullName ?? contact.UserName ?? "User",
                        ParentUserName = contact.UserName ?? "", Email = contact.Email ?? "", Role = contact.Role ?? "User" };
                }).OrderBy(x => x.Role == "Parent" ? 0 : x.Role == "Student" ? 1 : 2)
                  .ThenBy(x => x.ParentUserName, StringComparer.OrdinalIgnoreCase).ToList();
            }

            if (active != null)
            {
                model.Messages = await _context.ChatMessages
                    .AsNoTracking()
                    .Where(item => item.ConversationId == active.ConversationId)
                    .Where(item => !hiddenMessageIds.Contains(item.ChatMessageId))
                    .Include(item => item.SenderUser)
                    .Include(item => item.Attachments)
                    .OrderBy(item => item.SentAt)
                    .Select(item => new ChatMessageItemViewModel
                    {
                        ChatMessageId = item.ChatMessageId,
                        IsMine = item.SenderUserId == userId,
                        SenderName = item.SenderUser.FullName ?? item.SenderUser.UserName ?? "User",
                        Content = item.ProtectedContent ?? string.Empty,
                        SentAt = item.SentAt,
                        ReadAt = item.ReadAt,
                        EditedAt = item.EditedAt,
                        IsDeletedForEveryone = item.IsDeletedForEveryone,
                        Attachments = item.Attachments.Select(file =>
                            new MessageAttachmentViewModel
                            {
                                MessageAttachmentId = file.MessageAttachmentId,
                                FileName = file.OriginalFileName,
                                ContentType = file.ContentType,
                                FileSize = file.FileSize
                            }).ToList()
                    })
                    .ToListAsync();

                foreach (ChatMessageItemViewModel message in model.Messages)
                {
                    if (message.IsDeletedForEveryone)
                    {
                        message.Content = "This message was deleted";
                        message.Attachments.Clear();
                    }
                    else
                    {
                        message.Content = _protection.UnprotectText(message.Content);
                    }

                    bool withinWindow =
                        DateTime.UtcNow - message.SentAt <= MessageChangeWindow;

                    message.CanEdit =
                        message.IsMine &&
                        withinWindow &&
                        !message.ReadAt.HasValue &&
                        !message.IsDeletedForEveryone &&
                        !string.IsNullOrWhiteSpace(message.Content);

                    message.CanDeleteForEveryone =
                        message.IsMine &&
                        withinWindow &&
                        !message.IsDeletedForEveryone;
                }
            }

            return View(model);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> UnreadCounts()
        {
            var counts = await MessageUnreadQuery.ForUser(
                    _context, CurrentUserId(), User.IsInRole("Admin"))
                .GroupBy(message => message.ConversationId)
                .Select(group => new { conversationId = group.Key, unreadCount = group.Count() })
                .ToListAsync();

            return Json(new { totalUnreadCount = counts.Sum(item => item.unreadCount), conversations = counts });
        }

        [HttpGet]
        public async Task<IActionResult> ReadStatuses(int conversationId)
        {
            string userId = CurrentUserId();
            bool isAdmin = User.IsInRole("Admin");

            bool canAccess = await AccessibleConversations(userId, isAdmin)
                .AsNoTracking()
                .AnyAsync(item => item.ConversationId == conversationId);

            if (!canAccess)
            {
                return NotFound();
            }

            List<int> readMessageIds = await _context.ChatMessages
                .AsNoTracking()
                .Where(item =>
                    item.ConversationId == conversationId &&
                    item.SenderUserId == userId &&
                    item.ReadAt.HasValue)
                .Select(item => item.ChatMessageId)
                .ToListAsync();

            return Json(new { readMessageIds });
        }

        [Authorize(Roles = "Parent,Student,Tutor"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenSupportFromChatbot(string? handoffToken, bool confirmed)
        {
            if (!confirmed) return BadRequest("Confirm that the question may be shared with staff.");
            var draft = _chatbotSessions.ReadHandoff(handoffToken, CurrentUserId());
            if (draft == null) return BadRequest("The support draft expired. Open FAQ & Support and try again.");
            var conversation = await GetOrCreateParentConversation(CurrentUserId());
            // This opens a draft only. The user reviews and presses Send in Messages.
            TempData["ChatbotSupportDraft"] = $"Support request from FAQ & Support\n\nQuestion: {draft.Question}\n\nAssistant response (not staff approval): {draft.Answer}";
            return RedirectToAction(nameof(Index), new { id = conversation.ConversationId });
        }

        [Authorize(Roles = "Parent,Student,Tutor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartParent()
        {
            var conversation = await GetOrCreateParentConversation(CurrentUserId());
            return RedirectToAction(nameof(Index), new { id = conversation.ConversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int conversationId, int lastVisibleMessageId)
        {
            string userId = CurrentUserId();
            if (!await AccessibleConversations(userId, User.IsInRole("Admin"))
                .AnyAsync(x => x.ConversationId == conversationId)) return NotFound();
            bool isAdmin = User.IsInRole("Admin");
            var unread = await _context.ChatMessages.Where(x =>
                x.ConversationId == conversationId && x.ChatMessageId <= lastVisibleMessageId &&
                (isAdmin ? x.SenderUserId == x.Conversation.ParentUserId : x.SenderUserId != userId) && !x.IsDeletedForEveryone && !x.ReadAt.HasValue &&
                !_context.MessageUserDeletions.Any(d => d.ChatMessageId == x.ChatMessageId && d.UserId == userId))
                .ToListAsync();
            var now = DateTime.UtcNow;
            unread.ForEach(x => x.ReadAt = now);
            if (unread.Count > 0) await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(string parentUserId)
        {
            bool validContact = await (from user in _context.Users
                join membership in _context.UserRoles on user.Id equals membership.UserId
                join role in _context.Roles on membership.RoleId equals role.Id
                where user.Id == parentUserId && user.IsActive &&
                    (role.Name == "Parent" || role.Name == "Student" || role.Name == "Tutor")
                select user.Id).AnyAsync();
            if (!validContact) return NotFound();

            Conversation conversation = await GetOrCreateParentConversation(parentUserId);

            return RedirectToAction(nameof(Index), new { id = conversation.ConversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaximumFileSize + 1024 * 1024)]
        public async Task<IActionResult> Send(SendMessageViewModel model)
        {
            string userId = CurrentUserId();
            bool isAdmin = User.IsInRole("Admin");

            Conversation? conversation = await AccessibleConversations(userId, isAdmin)
                .FirstOrDefaultAsync(item => item.ConversationId == model.ConversationId);

            if (conversation == null)
            {
                return NotFound();
            }

            string content = (model.Content ?? string.Empty).Trim();
            if (content.Length > 2000)
            {
                ModelState.AddModelError(nameof(model.Content), "Message cannot exceed 2,000 characters.");
            }

            if (string.IsNullOrWhiteSpace(content) && model.Attachment == null)
            {
                ModelState.AddModelError(nameof(model.Content), "Enter a message or select a file.");
            }

            if (model.Attachment != null && !IsValidAttachment(model.Attachment, out string fileError))
            {
                ModelState.AddModelError(nameof(model.Attachment), fileError);
                await _events.RecordAsync(HttpContext, "UploadRejected", SecuritySeverity.Warning,
                    "Message attachment rejected by the file validation policy.");
            }

            if (!ModelState.IsValid)
            {
                TempData["MessageError"] = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault() ?? "The message could not be sent.";
                return RedirectToAction(nameof(Index), new { id = model.ConversationId });
            }

            var message = new ChatMessage
            {
                ConversationId = conversation.ConversationId,
                SenderUserId = userId,
                ProtectedContent = string.IsNullOrWhiteSpace(content)
                    ? null
                    : _protection.ProtectText(content)
            };

            if (model.Attachment != null)
            {
                await AddAttachment(message, model.Attachment);
            }

            conversation.LastMessageAt = message.SentAt;
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { id = model.ConversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMessage(EditMessageViewModel model)
        {
            await using var editTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            string userId = CurrentUserId();
            ChatMessage? message = await _context.ChatMessages
                .Include(item => item.Conversation)
                .FirstOrDefaultAsync(item =>
                    item.ChatMessageId == model.ChatMessageId);

            if (message == null || message.SenderUserId != userId)
            {
                return NotFound();
            }

            if (message.ReadAt.HasValue)
            {
                TempData["MessageError"] =
                    "Read messages can no longer be edited.";
                return RedirectToAction(nameof(Index),
                    new { id = message.ConversationId });
            }

            if (message.IsDeletedForEveryone ||
                DateTime.UtcNow - message.SentAt > MessageChangeWindow)
            {
                TempData["MessageError"] =
                    "Messages can only be edited within 15 minutes after sending.";
                return RedirectToAction(nameof(Index),
                    new { id = message.ConversationId });
            }

            string content = (model.Content ?? string.Empty).Trim();
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(content))
            {
                TempData["MessageError"] =
                    "Edited message content is required and cannot exceed 2,000 characters.";
                return RedirectToAction(nameof(Index),
                    new { id = message.ConversationId });
            }

            message.ProtectedContent = _protection.ProtectText(content);
            message.EditedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await editTransaction.CommitAsync();

            return RedirectToAction(nameof(Index),
                new { id = message.ConversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteForEveryone(int id)
        {
            string userId = CurrentUserId();
            ChatMessage? message = await _context.ChatMessages
                .FirstOrDefaultAsync(item => item.ChatMessageId == id);

            if (message == null || message.SenderUserId != userId)
            {
                return NotFound();
            }

            if (message.IsDeletedForEveryone ||
                DateTime.UtcNow - message.SentAt > MessageChangeWindow)
            {
                TempData["MessageError"] =
                    "Messages can only be deleted for everyone within 15 minutes after sending.";
                return RedirectToAction(nameof(Index),
                    new { id = message.ConversationId });
            }

            message.ProtectedContent = null;
            message.IsDeletedForEveryone = true;
            message.DeletedAt = DateTime.UtcNow;
            message.DeletedByUserId = userId;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index),
                new { id = message.ConversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteForMe(int id)
        {
            string userId = CurrentUserId();
            bool isAdmin = User.IsInRole("Admin");

            ChatMessage? message = await _context.ChatMessages
                .Include(item => item.Conversation)
                .FirstOrDefaultAsync(item => item.ChatMessageId == id);

            if (message == null ||
                (!isAdmin && message.Conversation.ParentUserId != userId))
            {
                return NotFound();
            }

            bool alreadyHidden = await _context.MessageUserDeletions
                .AnyAsync(item =>
                    item.ChatMessageId == id && item.UserId == userId);

            if (!alreadyHidden)
            {
                _context.MessageUserDeletions.Add(new MessageUserDeletion
                {
                    ChatMessageId = id,
                    UserId = userId
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index),
                new { id = message.ConversationId });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Download(int id, bool preview = false)
        {
            string userId = CurrentUserId();
            bool isAdmin = User.IsInRole("Admin");

            MessageAttachment? attachment = await _context.MessageAttachments
                .AsNoTracking()
                .Include(item => item.ChatMessage)
                    .ThenInclude(item => item.Conversation)
                .FirstOrDefaultAsync(item => item.MessageAttachmentId == id);

            if (attachment == null ||
                attachment.ChatMessage.IsDeletedForEveryone ||
                (!isAdmin && attachment.ChatMessage.Conversation.ParentUserId != userId) ||
                await _context.MessageUserDeletions.AsNoTracking().AnyAsync(item =>
                    item.ChatMessageId == attachment.ChatMessageId &&
                    item.UserId == userId))
            {
                return NotFound();
            }

            string path = Path.Combine(StorageDirectory(), attachment.StoredFileName);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            byte[] protectedBytes = await System.IO.File.ReadAllBytesAsync(path);
            byte[] bytes;
            try
            {
                bytes = _protection.UnprotectFile(protectedBytes);
            }
            catch
            {
                return NotFound();
            }

            if (preview)
            {
                if (attachment.ContentType is not ("image/png" or "image/jpeg")) return BadRequest();
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                return File(bytes, attachment.ContentType);
            }
            string safeName = Path.GetFileName(attachment.OriginalFileName);
            return File(bytes, "application/octet-stream", safeName);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConversationPreference(int conversationId, string operation, bool pinned = false, int? activeConversationId = null)
        {
            if (operation is not ("pin" or "delete")) return BadRequest();
            string userId = CurrentUserId();
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (!await AccessibleConversations(userId, User.IsInRole("Admin")).AnyAsync(x => x.ConversationId == conversationId)) return NotFound();
            var preference = await _context.ConversationUserPreferences.FirstOrDefaultAsync(x => x.UserId == userId && x.ConversationId == conversationId);
            if (preference == null)
            {
                preference = new ConversationUserPreference { UserId = userId, ConversationId = conversationId };
                _context.ConversationUserPreferences.Add(preference);
            }
            if (operation == "pin") preference.IsPinned = pinned;
            else { preference.IsHidden = true; preference.HiddenAtUtc = DateTime.UtcNow; preference.IsPinned = false; }
            await _context.SaveChangesAsync(); await tx.CommitAsync();
            return RedirectToAction(nameof(Index), new { id = operation == "delete" && activeConversationId == conversationId ? (int?)null : activeConversationId });
        }

        private IQueryable<Conversation> AccessibleConversations(string userId, bool isAdmin)
        {
            IQueryable<Conversation> query = _context.Conversations;
            return isAdmin ? query : query.Where(item => item.ParentUserId == userId);
        }

        private async Task<Conversation> GetOrCreateParentConversation(string userId)
        {
            Conversation? conversation = await _context.Conversations
                .FirstOrDefaultAsync(item => item.ParentUserId == userId);

            if (conversation != null)
            {
                return conversation;
            }

            conversation = new Conversation { ParentUserId = userId };
            _context.Conversations.Add(conversation);
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                var existing = await _context.Conversations.FirstOrDefaultAsync(x => x.ParentUserId == userId);
                if (existing == null) throw;
                return existing;
            }
            return conversation;
        }

        private async Task AddAttachment(ChatMessage message, IFormFile file)
        {
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            byte[] rawBytes = stream.ToArray();
            byte[] protectedBytes = _protection.ProtectFile(rawBytes);
            string storedName = $"{Guid.NewGuid():N}.bin";

            Directory.CreateDirectory(StorageDirectory());
            await System.IO.File.WriteAllBytesAsync(
                Path.Combine(StorageDirectory(), storedName), protectedBytes);

            message.Attachments.Add(new MessageAttachment
            {
                OriginalFileName = Path.GetFileName(file.FileName),
                StoredFileName = storedName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                Sha256Hash = Convert.ToHexString(SHA256.HashData(rawBytes))
            });
        }

        private static bool IsValidAttachment(IFormFile file, out string error)
        {
            error = string.Empty;
            if (file.Length <= 0 || file.Length > MaximumFileSize)
            {
                error = "The attachment must be between 1 byte and 10 MB.";
                return false;
            }

            string extension = Path.GetExtension(file.FileName);
            if (!AllowedFiles.TryGetValue(extension, out string[]? contentTypes) ||
                !contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                error = "Only PDF, JPG, PNG and DOCX files are allowed.";
                return false;
            }

            using Stream input = file.OpenReadStream();
            Span<byte> header = stackalloc byte[8];
            int read = input.Read(header);
            bool signatureMatches = extension.ToLowerInvariant() switch
            {
                ".pdf" => read >= 4 && header[..4].SequenceEqual("%PDF"u8),
                ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => read >= 8 && header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                ".docx" => read >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,
                _ => false
            };

            if (!signatureMatches)
            {
                error = "The selected file content does not match its extension.";
                return false;
            }
            if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using Stream document = file.OpenReadStream();
                    using var archive = new ZipArchive(document, ZipArchiveMode.Read);
                    if (archive.Entries.Count > 2000 || archive.GetEntry("[Content_Types].xml") == null ||
                        archive.GetEntry("word/document.xml") == null ||
                        archive.Entries.Any(x => x.Length > 20 * 1024 * 1024 ||
                            x.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)) ||
                        archive.Entries.Sum(x => x.Length) > 50L * 1024 * 1024)
                    {
                        error = "The selected DOCX is invalid or exceeds the document safety limits.";
                        return false;
                    }
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException)
                {
                    error = "The selected DOCX file is invalid.";
                    return false;
                }
            }
            return true;
        }

        private string StorageDirectory() =>
            Path.Combine(_environment.ContentRootPath, "App_Data", "MessageAttachments");

        private string CurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The signed-in user could not be identified.");

        private static string Preview(string content, bool hasAttachment)
        {
            string value = string.IsNullOrWhiteSpace(content)
                ? hasAttachment ? "Attachment" : "No message"
                : content.ReplaceLineEndings(" ");
            return value.Length <= 45 ? value : value[..42] + "...";
        }
    }
}
