using inflan_api.DTOs;
using inflan_api.Models;

namespace inflan_api.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request);
        Task<NotificationListResponse> GetNotificationsAsync(int userId, int page = 1, int pageSize = 20);
        Task<int> GetUnreadCountAsync(int userId);
        Task<bool> MarkAsReadAsync(int notificationId, int userId);
        Task<bool> MarkAllAsReadAsync(int userId);
        Task<bool> MarkMultipleAsReadAsync(List<int> notificationIds, int userId);
        Task<bool> DeleteNotificationAsync(int notificationId, int userId);
        Task<bool> DeleteAllNotificationsAsync(int userId);

        // Helper methods for creating specific notification types
        Task<NotificationDto> CreateMessageNotificationAsync(int recipientId, int senderId, string senderName, int conversationId, string messagePreview);
        Task<NotificationDto> CreateCampaignNotificationAsync(int userId, int campaignId, string campaignName, int type, string message);
    }
}
