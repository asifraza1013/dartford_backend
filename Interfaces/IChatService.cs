using inflan_api.Models;
using inflan_api.DTOs;

namespace inflan_api.Interfaces
{
    public interface IChatService
    {
        // Conversation methods
        Task<(bool Success, string Message, Conversation? Conversation)> GetOrCreateConversation(int userId, int otherUserId);
        Task<ConversationDto?> GetConversationById(int conversationId, int userId);
        Task<IEnumerable<ConversationDto>> GetUserConversations(int userId);
        Task<(bool Success, string Message)> DeleteConversation(int conversationId, int userId);

        // Message methods
        Task<(bool Success, string Message, MessageDto? MessageDto)> SendMessage(int senderId, int conversationId, string? content, int messageType = 1);
        Task<(bool Success, string Message, MessageDto? MessageDto)> SendMessageWithAttachment(int senderId, int conversationId, string? content, IFormFile file);
        Task<IEnumerable<MessageDto>> GetMessages(int conversationId, int userId, int page = 1, int pageSize = 50);
        Task<int> GetUnreadCount(int userId);
        Task<(bool Success, string Message)> MarkAsRead(int conversationId, int userId);
        Task<(bool Success, string Message)> DeleteMessage(int messageId, int userId, bool deleteForEveryone = false);

        // File handling
        Task<string?> SaveChatAttachment(IFormFile file);
    }
}
