using SecureSmartTuitionSystem.Services.Tables;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;
using SecureSmartTuitionSystem.Services.Communication;
using SecureSmartTuitionSystem.ViewModels.Communication;
namespace SecureSmartTuitionSystem.Controllers;
[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GuestEnquiriesController(ApplicationDbContext db, IMessageProtectionService protection, IGuestEnquiryEmailSender email) : Controller
{
    private GuestEnquiryRow Row(GuestEnquiry item)
    {
        GuestContact contact;
        try { contact = JsonSerializer.Deserialize<GuestContact>(protection.UnprotectText(item.ProtectedContact)) ?? new("Unavailable", "", "Contact unavailable."); }
        catch (JsonException) { contact = new("Unavailable", "", "Contact unavailable. Check Data Protection keys."); }
        return new(item.GuestEnquiryId, item.CreatedAtUtc, item.IsResolved, contact, item.ReadAtUtc,
            item.ReplyState, item.ReplySentAtUtc, item.ProtectedReply == null ? "" : protection.UnprotectText(item.ProtectedReply), item.ReplyAttemptId, item.ReplyClaimedAtUtc, item.ProtectedRecoveryHistory == null ? "" : protection.UnprotectText(item.ProtectedRecoveryHistory), item.ProtectedConversation == null ? null : JsonSerializer.Deserialize<List<GuestEnquiryTurn>>(protection.UnprotectText(item.ProtectedConversation)));
    }
    [HttpGet] public async Task<IActionResult> Index(string status = "all", int page = 1, CancellationToken ct = default)
    {
        status = status is "unread" or "awaiting" or "resolved" ? status : "all";
        page = Math.Clamp(page, 1, 100000);
        int total = await db.GuestEnquiries.CountAsync(ct);
        int resolved = await db.GuestEnquiries.CountAsync(x => x.IsResolved, ct);
        int unread = await db.GuestEnquiries.CountAsync(x => !x.IsResolved && x.ReadAtUtc == null, ct);
        var query = db.GuestEnquiries.AsNoTracking();
        query = status switch { "unread" => query.Where(x => !x.IsResolved && x.ReadAtUtc == null),
            "awaiting" => query.Where(x => !x.IsResolved && x.ReadAtUtc != null),
            "resolved" => query.Where(x => x.IsResolved), _ => query };
        int filteredTotal = status switch { "unread" => unread, "awaiting" => total - resolved - unread, "resolved" => resolved, _ => total };
        page = Math.Min(page, Math.Max(1, (filteredTotal + 24) / 25));
        var sort = HttpContext?.Request.Query["tableSort"].ToString();
        List<GuestEnquiryRow> rows;
        if (sort is "name" or "email")
        {
            // SQL cannot alphabetically sort an encrypted payload. Read only
            // ID/contact projections server-side; decrypt only Name/Email.
            var contacts = await query.Select(x => new { x.GuestEnquiryId, x.ProtectedContact }).ToListAsync(ct);
            var keys = contacts.Select(x => {
                var contact = JsonSerializer.Deserialize<GuestContact>(protection.UnprotectText(x.ProtectedContact));
                return new { Id = x.GuestEnquiryId, Name = contact?.Name ?? "", Email = contact?.Email ?? "" };
            });
            var ids = TableOrder.Local(keys, HttpContext?.Request.Query, "name", false,
                ("name", x => x.Name), ("email", x => x.Email)).ThenBy(x => x.Id)
                .Skip((page - 1) * 25).Take(25).Select(x => x.Id).ToList();
            var selected = await query.Where(x => ids.Contains(x.GuestEnquiryId)).ToListAsync(ct);
            rows = selected.OrderBy(x => ids.IndexOf(x.GuestEnquiryId)).Select(Row).ToList();
        }
        else
        {
            var items = await TableOrder.Apply(query, HttpContext?.Request.Query, "received", true,
                ("received", x => x.CreatedAtUtc), ("status", x => x.IsResolved ? "Resolved" : x.ReadAtUtc != null ? "Awaiting reply" : "Unread"))
                .ThenByDescending(x => x.GuestEnquiryId).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
            rows = items.Select(Row).ToList();
        }
        return View(new GuestInbox(rows, status, page, page * 25 < filteredTotal,
            total, unread, total - resolved - unread, resolved));
    }
    [HttpGet] public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        // Reading is monotonic and never changes a resolved/sent reply back to unread.
        await db.GuestEnquiries.Where(x => x.GuestEnquiryId == id && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReadAtUtc, DateTime.UtcNow), ct);
        var item = await db.GuestEnquiries.AsNoTracking().SingleOrDefaultAsync(x => x.GuestEnquiryId == id, ct);
        return item == null ? NotFound() : View(new GuestEnquiryDetails(Row(item), new() { Id = id, Reply = Row(item).ReplyText, ConversationVersion = Row(item).Conversation?.Count ?? 0 }));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(GuestReplyForm model, CancellationToken ct)
    {
        var item = await db.GuestEnquiries.AsNoTracking().SingleOrDefaultAsync(x => x.GuestEnquiryId == model.Id, ct);
        if (item == null) return NotFound();
        var row = Row(item);
        if (model.ConversationVersion != (row.Conversation?.Count ?? 0)) {
            TempData["GuestReplyError"] = "A new follow-up was recorded. Reload and review the latest question before replying.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
        model.Reply = (model.Reply ?? "").Trim();
        if (model.Reply.Length < 5 || model.Reply.Length > 4000)
            ModelState.AddModelError(nameof(model.Reply), model.Reply.Length == 0 ? "Reply field is required." : "Reply must contain 5–4000 characters.");
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(row.Contact.Email) || row.Contact.Email.Any(char.IsControl))
            ModelState.AddModelError("", "The saved contact email is unavailable or invalid.");
        if (!email.IsConfigured) ModelState.AddModelError("", "Email is not configured. Complete the existing Email SMTP settings before sending.");
        if (!ModelState.IsValid) return View("Details", new GuestEnquiryDetails(row, model));
        var attempt = Guid.NewGuid();
        string protectedReply = protection.ProtectText(model.Reply);
        int claimed = await db.GuestEnquiries.Where(x => x.GuestEnquiryId == model.Id && !x.IsResolved && x.ProtectedConversation == item.ProtectedConversation &&
                (x.ReplyState == GuestReplyState.None || x.ReplyState == GuestReplyState.Failed))
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReplyState, GuestReplyState.Sending).SetProperty(p => p.ReplyClaimedAtUtc, DateTime.UtcNow)
                .SetProperty(p => p.ReplyAttemptId, attempt).SetProperty(p => p.ProtectedReply, protectedReply)
                .SetProperty(p => p.ReadAtUtc, p => p.ReadAtUtc ?? DateTime.UtcNow), ct);
        if (claimed == 0) { TempData["GuestReplyError"] = "This enquiry is already resolved, sending, or awaiting delivery confirmation. No additional email was sent."; return RedirectToAction(nameof(Details), new { id = model.Id }); }
        try
        {
            // Complete and record an accepted send even if the browser disconnects.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await email.SendAsync(row.Contact.Email, row.Contact.Name, row.Contact.Message, model.Reply, model.Id, timeout.Token);
            int recorded = await db.GuestEnquiries.Where(x => x.GuestEnquiryId == model.Id && x.ReplyAttemptId == attempt && x.ReplyState == GuestReplyState.Sending)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReplyState, GuestReplyState.Sent)
                    .SetProperty(p => p.ReplySentAtUtc, DateTime.UtcNow).SetProperty(p => p.IsResolved, true)
                    .SetProperty(p => p.ResolvedAtUtc, DateTime.UtcNow), CancellationToken.None);
            TempData[recorded == 1 ? "GuestReplySuccess" : "GuestReplyError"] = recorded == 1
                ? "Reply sent successfully. This enquiry is resolved." : "The mail server accepted the reply, but its record changed. Check the delivery history before continuing.";
        }
        catch (SmtpFailedRecipientException)
        {
            await SetAttemptState(model.Id, attempt, GuestReplyState.Failed);
            TempData["GuestReplyError"] = "The mail server rejected the recipient. The enquiry remains awaiting reply. Check the email before retrying.";
        }
        catch (Exception ex) when (ex is SmtpException or IOException or OperationCanceledException or DbUpdateException or Microsoft.Data.SqlClient.SqlException)
        {
            // A network failure or a post-send database failure does not prove non-delivery.
            await SetAttemptState(model.Id, attempt, GuestReplyState.Unconfirmed);
            TempData["GuestReplyError"] = "Sending could not be confirmed. The enquiry is not resolved. Check the mail server before sending another reply; an automatic retry could send a duplicate.";
        }
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Recover(int id, Guid attemptId, string outcome, string reason, bool confirmed, DateTime? sentAtLocal, CancellationToken ct)
    {
        var item = await db.GuestEnquiries.AsNoTracking().SingleOrDefaultAsync(x => x.GuestEnquiryId == id, ct);
        if (item == null) return NotFound();
        var claimed = item.ReplyClaimedAtUtc ?? item.CreatedAtUtc;
        if (!confirmed || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500 || outcome is not ("sent" or "not-sent") ||
            item.IsResolved || item.ReplyAttemptId != attemptId || attemptId == Guid.Empty ||
            item.ReplyState is not (GuestReplyState.Sending or GuestReplyState.Unconfirmed) || claimed > DateTime.UtcNow.AddMinutes(-15) ||
            (outcome == "sent" && (!sentAtLocal.HasValue || sentAtLocal.Value.AddHours(-8) > DateTime.UtcNow || sentAtLocal.Value.AddHours(-8) < claimed.AddMinutes(-1))))
        { TempData["GuestReplyError"] = "Check the mail server, wait at least 15 minutes after the attempt, and provide a valid outcome, time and reason."; return RedirectToAction(nameof(Details), new { id }); }
        var history = item.ProtectedRecoveryHistory == null ? "" : protection.UnprotectText(item.ProtectedRecoveryHistory);
        history += $"{DateTime.UtcNow:O} | Admin {User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value} | Attempt {attemptId} | {outcome} | {reason.Trim()}\n";
        var protectedHistory = protection.ProtectText(history);
        var sent = outcome == "sent";
        DateTime? sentUtc = sent ? sentAtLocal!.Value.AddHours(-8) : null;
        int changed = await db.GuestEnquiries.Where(x => x.GuestEnquiryId == id && !x.IsResolved && x.ReplyAttemptId == attemptId &&
            x.ReplyState == item.ReplyState && x.ReplyClaimedAtUtc == item.ReplyClaimedAtUtc && x.ProtectedRecoveryHistory == item.ProtectedRecoveryHistory)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReplyState, sent ? GuestReplyState.Sent : GuestReplyState.Failed)
                .SetProperty(p => p.IsResolved, sent).SetProperty(p => p.ResolvedAtUtc, sent ? (DateTime?)DateTime.UtcNow : null)
                .SetProperty(p => p.ReplySentAtUtc, sentUtc).SetProperty(p => p.ProtectedRecoveryHistory, protectedHistory), ct);
        TempData[changed == 1 ? "GuestReplySuccess" : "GuestReplyError"] = changed != 1 ? "This enquiry changed. Reload before reviewing it again."
            : sent ? "The confirmed sent reply has been recorded. This enquiry is resolved." : "Confirmed not sent. Review the saved reply before sending a new attempt.";
        return RedirectToAction(nameof(Details), new { id });
    }
    // Incoming mail is reviewed in the centre mailbox. Staff explicitly record
    // the verified follow-up; this endpoint does not pretend to sync a mailbox.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id, Guid attemptId, string followUp, DateTime? receivedAtLocal, bool confirmed, CancellationToken ct)
    {
        var item = await db.GuestEnquiries.AsNoTracking().SingleOrDefaultAsync(x => x.GuestEnquiryId == id, ct);
        if (item == null) return NotFound();
        followUp = (followUp ?? "").Trim();
        var received = receivedAtLocal?.AddHours(-8);
        // Mailbox timestamps and datetime-local commonly have minute precision.
        var sentMinute = item.ReplySentAtUtc.HasValue ? item.ReplySentAtUtc.Value.AddTicks(-(item.ReplySentAtUtc.Value.Ticks % TimeSpan.TicksPerMinute)) : (DateTime?)null;
        if (!confirmed || followUp.Length is < 5 or > 2000 || !received.HasValue || received > DateTime.UtcNow ||
            !item.IsResolved || item.ReplyState != GuestReplyState.Sent || item.ReplyAttemptId != attemptId ||
            attemptId == Guid.Empty || !item.ReplySentAtUtc.HasValue || received < sentMinute)
        { TempData["GuestReplyError"] = "Verify the sender in the centre mailbox and enter the follow-up with its received time (after the previous reply)."; return RedirectToAction(nameof(Details), new { id }); }
        var row = Row(item);
        var history = row.Conversation?.ToList() ?? new List<GuestEnquiryTurn>();
        history.Add(new(row.Contact.Message, row.ReplyText, item.ReplySentAtUtc, received.Value, DateTime.UtcNow,
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Admin"));
        var conversation = protection.ProtectText(JsonSerializer.Serialize(history));
        var contact = protection.ProtectText(JsonSerializer.Serialize(row.Contact with { Message = followUp }));
        int changed = await db.GuestEnquiries.Where(x => x.GuestEnquiryId == id && x.IsResolved &&
            x.ReplyState == GuestReplyState.Sent && x.ReplyAttemptId == attemptId && x.ProtectedConversation == item.ProtectedConversation)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ProtectedConversation, conversation).SetProperty(p => p.ProtectedContact, contact)
                .SetProperty(p => p.IsResolved, false).SetProperty(p => p.ResolvedAtUtc, (DateTime?)null)
                .SetProperty(p => p.ReplyState, GuestReplyState.None).SetProperty(p => p.ReplyAttemptId, (Guid?)null)
                .SetProperty(p => p.ReplySentAtUtc, (DateTime?)null).SetProperty(p => p.ReplyClaimedAtUtc, (DateTime?)null)
                .SetProperty(p => p.ProtectedReply, (string?)null).SetProperty(p => p.ReadAtUtc, DateTime.UtcNow), ct);
        TempData[changed == 1 ? "GuestReplySuccess" : "GuestReplyError"] = changed == 1
            ? "Follow-up recorded. This enquiry is awaiting reply; previous replies are retained below."
            : "This enquiry changed. Reload before recording the follow-up again.";
        return RedirectToAction(nameof(Details), new { id });
    }
    private Task<int> SetAttemptState(int id, Guid attempt, GuestReplyState state) => db.GuestEnquiries
        .Where(x => x.GuestEnquiryId == id && x.ReplyAttemptId == attempt && !x.IsResolved)
        .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReplyState, state), CancellationToken.None);
}
