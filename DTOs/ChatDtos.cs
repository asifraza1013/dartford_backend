namespace inflan_api.DTOs
{
    public class ConversationDto
    {
        public int Id { get; set; }
        public int BrandId { get; set; }
        public string? BrandName { get; set; }
        public string? BrandProfileImage { get; set; }
        public int InfluencerId { get; set; }
        public string? InfluencerName { get; set; }
        public string? InfluencerProfileImage { get; set; }
        public int? CampaignId { get; set; }
        public string? CampaignName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public MessageDto? LastMessage { get; set; }
        public int UnreadCount { get; set; }

        // Helper properties for the current user
        public int OtherUserId { get; set; }
        public string? OtherUserName { get; set; }
        public string? OtherUserProfileImage { get; set; }
    }

    public class MessageDto
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string? SenderName { get; set; }
        public string? SenderProfileImage { get; set; }
        public int RecipientId { get; set; }
        public string? Content { get; set; }
        public int MessageType { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public long? AttachmentSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsMine { get; set; }
    }

    public class SendMessageRequest
    {
        public int ConversationId { get; set; }
        public string? Content { get; set; }
        public int MessageType { get; set; } = 1; // Default to text
    }

    public class StartConversationRequest
    {
        public int OtherUserId { get; set; }
        public string? InitialMessage { get; set; }
    }

    public class TypingIndicatorDto
    {
        public int ConversationId { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public bool IsTyping { get; set; }
    }

    public class OnlineStatusDto
    {
        public int UserId { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
    }
}
