namespace inflan_api.DTOs
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public int? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
        public string? Data { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NotificationListResponse
    {
        public List<NotificationDto> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class CreateNotificationRequest
    {
        public int UserId { get; set; }
        public int Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public int? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
        public string? Data { get; set; }
    }

    public class MarkNotificationsReadRequest
    {
        public List<int>? NotificationIds { get; set; } // If null, mark all as read
    }
}
