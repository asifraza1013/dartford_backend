using Microsoft.EntityFrameworkCore;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;

namespace inflan_api.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly InflanDBContext _context;

        public ChatRepository(InflanDBContext context)
        {
            _context = context;
        }

        #region Conversation Methods

        public async Task<Conversation> CreateConversation(Conversation conversation)
        {
            conversation.CreatedAt = DateTime.UtcNow;
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
            return conversation;
        }

        public async Task<Conversation?> GetConversationById(int id)
        {
            return await _context.Conversations.FindAsync(id);
        }

        public async Task<Conversation?> GetConversationByParticipants(int brandId, int influencerId)
        {
            return await _context.Conversations
                .FirstOrDefaultAsync(c => c.BrandId == brandId && c.InfluencerId == influencerId);
        }

        public async Task<IEnumerable<Conversation>> GetConversationsByUserId(int userId)
        {
            return await _context.Conversations
                .Where(c => (c.BrandId == userId && !c.IsDeletedByBrand) ||
                           (c.InfluencerId == userId && !c.IsDeletedByInfluencer))
                .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateConversation(Conversation conversation)
        {
            _context.Conversations.Update(conversation);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteConversationForUser(int conversationId, int userId)
        {
            var conversation = await _context.Conversations.FindAsync(conversationId);
            if (conversation == null) return false;

            // Check if user is part of the conversation
            if (conversation.BrandId == userId)
            {
                conversation.IsDeletedByBrand = true;
            }
            else if (conversation.InfluencerId == userId)
            {
                conversation.IsDeletedByInfluencer = true;
            }
            else
            {
                return false;
            }

            // Also mark all messages as deleted for this user
            var messages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .ToListAsync();

            foreach (var message in messages)
            {
                if (message.SenderId == userId)
                    message.IsDeletedBySender = true;
                if (message.RecipientId == userId)
                    message.IsDeletedByRecipient = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        #endregion

        #region ChatMessage Methods

        public async Task<ChatMessage> CreateMessage(ChatMessage message)
        {
            message.CreatedAt = DateTime.UtcNow;
            _context.ChatMessages.Add(message);

            // Update conversation's last message timestamp
            var conversation = await _context.Conversations.FindAsync(message.ConversationId);
            if (conversation != null)
            {
                conversation.LastMessageAt = message.CreatedAt;

                // If conversation was deleted by recipient, restore it
                if (conversation.BrandId == message.RecipientId)
                    conversation.IsDeletedByBrand = false;
                else if (conversation.InfluencerId == message.RecipientId)
                    conversation.IsDeletedByInfluencer = false;
            }

            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<ChatMessage?> GetMessageById(int id)
        {
            return await _context.ChatMessages.FindAsync(id);
        }

        public async Task<IEnumerable<ChatMessage>> GetMessagesByConversationId(int conversationId, int userId, int page = 1, int pageSize = 50)
        {
            return await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .Where(m => !((m.SenderId == userId && m.IsDeletedBySender) ||
                             (m.RecipientId == userId && m.IsDeletedByRecipient)))
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.CreatedAt) // Re-order ascending for display
                .ToListAsync();
        }

        public async Task<int> GetUnreadMessageCount(int conversationId, int userId)
        {
            return await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .Where(m => m.RecipientId == userId && !m.IsRead)
                .Where(m => !m.IsDeletedByRecipient)
                .CountAsync();
        }

        public async Task<int> GetTotalUnreadMessageCount(int userId)
        {
            return await _context.ChatMessages
                .Where(m => m.RecipientId == userId && !m.IsRead)
                .Where(m => !m.IsDeletedByRecipient)
                .CountAsync();
        }

        public async Task MarkMessagesAsRead(int conversationId, int userId)
        {
            var unreadMessages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .Where(m => m.RecipientId == userId && !m.IsRead)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteMessageForUser(int messageId, int userId)
        {
            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message == null) return false;

            if (message.SenderId == userId)
            {
                message.IsDeletedBySender = true;
            }
            else if (message.RecipientId == userId)
            {
                message.IsDeletedByRecipient = true;
            }
            else
            {
                return false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteMessageForEveryone(int messageId, int senderId)
        {
            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message == null || message.SenderId != senderId) return false;

            // Mark as deleted for both parties
            message.IsDeletedBySender = true;
            message.IsDeletedByRecipient = true;

            await _context.SaveChangesAsync();
            return true;
        }

        #endregion
    }
}
