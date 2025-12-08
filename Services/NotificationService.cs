using System.Text.Json;
using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;

namespace inflan_api.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;

        public NotificationService(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request)
        {
            var notification = new Notification
            {
                UserId = request.UserId,
                Type = request.Type,
                Title = request.Title,
                Message = request.Message,
                ReferenceId = request.ReferenceId,
                ReferenceType = request.ReferenceType,
                Data = request.Data,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _notificationRepository.CreateAsync(notification);
            return MapToDto(created);
        }

        public async Task<NotificationListResponse> GetNotificationsAsync(int userId, int page = 1, int pageSize = 20)
        {
            var notifications = await _notificationRepository.GetByUserIdAsync(userId, page, pageSize);
            var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId);
            var totalCount = await _notificationRepository.GetTotalCountAsync(userId);

            return new NotificationListResponse
            {
                Notifications = notifications.Select(MapToDto).ToList(),
                UnreadCount = unreadCount,
                TotalCount = totalCount
            };
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _notificationRepository.GetUnreadCountAsync(userId);
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            return await _notificationRepository.MarkAsReadAsync(notificationId, userId);
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            return await _notificationRepository.MarkAllAsReadAsync(userId);
        }

        public async Task<bool> MarkMultipleAsReadAsync(List<int> notificationIds, int userId)
        {
            return await _notificationRepository.MarkMultipleAsReadAsync(notificationIds, userId);
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId, int userId)
        {
            return await _notificationRepository.DeleteAsync(notificationId, userId);
        }

        public async Task<bool> DeleteAllNotificationsAsync(int userId)
        {
            return await _notificationRepository.DeleteAllAsync(userId);
        }

        public async Task<NotificationDto> CreateMessageNotificationAsync(int recipientId, int senderId, string senderName, int conversationId, string messagePreview)
        {
            // Truncate message preview if too long
            if (messagePreview.Length > 100)
            {
                messagePreview = messagePreview.Substring(0, 97) + "...";
            }

            var data = JsonSerializer.Serialize(new
            {
                senderId,
                senderName,
                conversationId
            });

            var request = new CreateNotificationRequest
            {
                UserId = recipientId,
                Type = NotificationType.Message,
                Title = $"New message from {senderName}",
                Message = messagePreview,
                ReferenceId = conversationId,
                ReferenceType = "conversation",
                Data = data
            };

            return await CreateNotificationAsync(request);
        }

        public async Task<NotificationDto> CreateCampaignNotificationAsync(int userId, int campaignId, string campaignName, int type, string message)
        {
            var data = JsonSerializer.Serialize(new
            {
                campaignId,
                campaignName
            });

            string title = type switch
            {
                NotificationType.CampaignUpdate => $"Campaign Update: {campaignName}",
                NotificationType.CampaignInvite => $"Campaign Invitation",
                NotificationType.CampaignApplication => $"New Application for {campaignName}",
                _ => "Campaign Notification"
            };

            var request = new CreateNotificationRequest
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                ReferenceId = campaignId,
                ReferenceType = "campaign",
                Data = data
            };

            return await CreateNotificationAsync(request);
        }

        private static NotificationDto MapToDto(Notification notification)
        {
            return new NotificationDto
            {
                Id = notification.Id,
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                ReferenceId = notification.ReferenceId,
                ReferenceType = notification.ReferenceType,
                Data = notification.Data,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                CreatedAt = notification.CreatedAt
            };
        }
    }
}
