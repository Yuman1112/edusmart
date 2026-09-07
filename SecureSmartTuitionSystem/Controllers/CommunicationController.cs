using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Filters;
using SecureSmartTuitionSystem.Models;
using SecureSmartTuitionSystem.Services;
using SecureSmartTuitionSystem.ViewModels;

namespace SecureSmartTuitionSystem.Controllers;

/// <summary>
/// Communication & Announcement Module — chatbot (Table 4.14), messaging
/// (Table 4.15), and chat history (Table 4.16).
/// </summary>
[Authorize]
public class CommunicationController : Controller
{
    private readonly AppDbContext _db;
    private readonly IChatbotService _chatbot;
    private readonly IFileValidationService _fileValidation;
    private readonly INotificationService _notifications;

    public CommunicationController(AppDbContext db, IChatbotService chatbot,
        IFileValidationService fileValidation, INotificationService notifications)
    {
        _db = db;
        _chatbot = chatbot;
        _fileValidation = fileValidation;
        _notifications = notifications;
    }

    /// <summary>Table 4.14 "AI Chatbot FAQ" menu listing.</summary>
    [HttpGet]
    public async Task<IActionResult> ChatbotMenu()
    {
        var faqs = await _db.ChatbotFaqs
            .Where(f => f.IsActive)
            .OrderBy(f => f.MenuCategory).ThenBy(f => f.DisplayOrder)
            .Select(f => new { f.ChatbotFaqId, f.MenuCategory, f.Question })
            .ToListAsync();

        return Json(faqs);
    }

    /// <summary>Table 4.14 "AI Chatbot FAQ" — menu click or free-typed question.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromBody] ChatbotAskRequest request)
    {
        var userId = this.CurrentUserId()!;

        var response = request.SelectedFaqId is not null
            ? await _chatbot.AnswerByMenuSelectionAsync(request.SelectedFaqId.Value, userId)
            : await _chatbot.AnswerByFreeTextAsync(request.FreeTextQuestion ?? string.Empty, userId);

        if (!response.IsMatch)
        {
            // A1: "No exact match found for your query."
            return Ok(new
            {
                message = "No exact match found for your query.",
                canEscalate = true
            });
        }

        return Ok(new
        {
            answer = response.AnswerText,
            suggestedQuestions = response.SuggestedQuestions.Select(f => new { f.ChatbotFaqId, f.Question })
        });
    }

    /// <summary>Functional Requirements: "escalate an unresolved AI chatbot enquiry to a human staff member."</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Escalate(string questionText)
    {
        var userId = this.CurrentUserId()!;
        await _chatbot.EscalateAsync(questionText, userId);

        var adminIds = await _db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync();
        foreach (var adminId in adminIds)
        {
            await _notifications.SendOncePerDayAsync(adminId, NotificationType.ChatEscalation,
                "Chatbot enquiry escalated", $"An unresolved enquiry was escalated: \"{questionText}\"");
        }

        return Ok(new { message = "Your question has been forwarded to a staff member." });
    }

    /// <summary>Table 4.15 "Send and Receive Messages".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Audit("Communication.SendMessage", "Message")]
    public async Task<IActionResult> Send([FromForm] SendMessageRequest request, IFormFile? attachment)
    {
        if (!ModelState.IsValid)
        {
            // A1: "Message cannot be empty."
            return BadRequest(new { message = "Message cannot be empty." });
        }

        var senderId = this.CurrentUserId()!;
        int conversationId;

        if (request.ConversationId is not null)
        {
            conversationId = request.ConversationId.Value;
            var isParticipant = await _db.ConversationParticipants.AnyAsync(cp =>
                cp.ConversationId == conversationId && cp.UserId == senderId);
            if (!isParticipant) return Forbid();
        }
        else if (!string.IsNullOrEmpty(request.RecipientUserId))
        {
            var conversation = new Conversation();
            conversation.Participants.Add(new ConversationParticipant { UserId = senderId });
            conversation.Participants.Add(new ConversationParticipant { UserId = request.RecipientUserId });
            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();
            conversationId = conversation.ConversationId;
        }
        else
        {
            return BadRequest(new { message = "A conversationId or recipientUserId is required." });
        }

        var message = new Message
        {
            ConversationId = conversationId,
            SenderUserId = senderId,
            Body = request.Body // Razor auto-encodes on render (Ch.2.5 Output Encoding) - never stored/executed as HTML
        };

        if (attachment is not null)
        {
            var validation = _fileValidation.Validate(attachment);
            if (!validation.IsValid)
            {
                // A2: "Invalid attachment format or file size exceeds maximum limit."
                return BadRequest(new { message = validation.ErrorMessage });
            }

            var storagePath = Path.Combine("uploads", "messages", $"{Guid.NewGuid()}{Path.GetExtension(attachment.FileName)}");
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", storagePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using (var stream = System.IO.File.Create(fullPath))
            {
                await attachment.CopyToAsync(stream);
            }

            message.Attachments.Add(new MessageAttachment
            {
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                FileSizeBytes = (int)attachment.Length,
                StoragePath = storagePath
            });
        }

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        var recipients = await _db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != senderId)
            .Select(cp => cp.UserId)
            .ToListAsync();

        foreach (var recipientId in recipients)
        {
            // Real-time delivery would be pushed via SignalR here; IsDeliveredRealtime tracks whether
            // that push succeeded. If the recipient is offline, the Notification row still queues the
            // unread alert for delivery next login (Table 4.15 alternative flow A3).
            await _notifications.SendOncePerDayAsync(recipientId, NotificationType.NewMessage,
                "New message", "You have a new message.", message.MessageId);
        }

        return Ok(new { message.MessageId, message.SentAt });
    }

    /// <summary>Table 4.16 "Chat History Management".</summary>
    [HttpGet]
    public async Task<IActionResult> History(string? searchKeyword)
    {
        var userId = this.CurrentUserId()!;

        var query = _db.ConversationParticipants
            .Where(cp => cp.UserId == userId && !cp.IsDeletedForUser)
            .Include(cp => cp.Conversation).ThenInclude(c => c.Messages)
            .AsQueryable();

        var conversations = await query.ToListAsync();

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim().ToLowerInvariant();
            conversations = conversations
                .Where(cp => cp.Conversation.Messages.Any(m => m.Body.ToLowerInvariant().Contains(keyword)))
                .ToList();

            // A1: empty list -> "No matching messages found."
        }

        return Json(conversations.Select(cp => new
        {
            cp.ConversationId,
            cp.Conversation.Subject,
            cp.Conversation.IsArchived,
            LastMessageAt = cp.Conversation.Messages.OrderByDescending(m => m.SentAt).Select(m => m.SentAt).FirstOrDefault()
        }));
    }

    /// <summary>Table 4.16 "Archive Conversation".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int conversationId)
    {
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation is null) return NotFound();

        conversation.IsArchived = true;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Conversation archived." });
    }

    /// <summary>Table 4.16 "Delete Conversation" — soft delete, scoped to the requesting user only.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int conversationId)
    {
        var userId = this.CurrentUserId()!;
        var participant = await _db.ConversationParticipants.FirstOrDefaultAsync(cp =>
            cp.ConversationId == conversationId && cp.UserId == userId);

        if (participant is null) return NotFound();

        participant.IsDeletedForUser = true;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Conversation removed from your view." });
    }
}
