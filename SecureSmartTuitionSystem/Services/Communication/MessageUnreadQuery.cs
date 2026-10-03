using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Communication;

namespace SecureSmartTuitionSystem.Services.Communication
{
    public static class MessageUnreadQuery
    {
        public static IQueryable<ChatMessage> ForUser(
            ApplicationDbContext context, string userId, bool isAdmin)
        {
            return context.ChatMessages.AsNoTracking().Where(message =>
                (isAdmin || message.Conversation.ParentUserId == userId) &&
                (isAdmin ? message.SenderUserId == message.Conversation.ParentUserId : message.SenderUserId != userId) &&
                !message.ReadAt.HasValue &&
                !message.IsDeletedForEveryone &&
                !message.UserDeletions.Any(deletion => deletion.UserId == userId) &&
                !context.ConversationUserPreferences.Any(preference => preference.UserId == userId &&
                    preference.ConversationId == message.ConversationId && preference.IsHidden &&
                    preference.HiddenAtUtc.HasValue && message.SentAt <= preference.HiddenAtUtc.Value));
        }
    }
}
