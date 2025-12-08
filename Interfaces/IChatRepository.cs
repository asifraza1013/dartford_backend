using inflan_api.Models;

namespace inflan_api.Interfaces
{
    public interface IChatRepository
    {
        // Conversation methods
        Task<Conversation> CreateConversation(Conversation conversation);
        Task<Conversation?> GetConversationById(int id);
        Task<Conversation?> GetConversationByParticipants(int brandId, int influencerId);
        Task<IEnumerable<Conversation>> GetConversationsByUserId(int userId);
        Task UpdateConversation(Conversation conversation);
        Task<bool> DeleteConversationForUser(int conversationId, int userId);

        // ChatMessage methods
        Task<ChatMessage> CreateMessage(ChatMessage message);
        Task<ChatMessage?> GetMessageById(int id);
        Task<IEnumerable<ChatMessage>> GetMessagesByConversationId(int conversationId, int userId, int page = 1, int pageSize = 50);
        Task<int> GetUnreadMessageCount(int conversationId, int userId);
        Task<int> GetTotalUnreadMessageCount(int userId);
        Task MarkMessagesAsRead(int conversationId, int userId);
        Task<bool> DeleteMessageForUser(int messageId, int userId);
        Task<bool> DeleteMessageForEveryone(int messageId, int senderId);
    }
}
