using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace inflan_api.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ConversationId { get; set; }

        [Required]
        public int SenderId { get; set; }

        [Required]
        public int RecipientId { get; set; }

        // Message content - can be null if it's a file-only message
        public string? Content { get; set; }

        // Message type: 1 = Text, 2 = Image, 3 = File, 4 = Video
        public int MessageType { get; set; } = 1;

        // For file/image attachments
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public long? AttachmentSize { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Read receipt
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        // Soft delete flags for each party
        public bool IsDeletedBySender { get; set; } = false;
        public bool IsDeletedByRecipient { get; set; } = false;

        // Navigation property
        [ForeignKey("ConversationId")]
        public virtual Conversation? Conversation { get; set; }
    }

    // Message type constants
    public static class ChatMessageType
    {
        public const int Text = 1;
        public const int Image = 2;
        public const int File = 3;
        public const int Video = 4;
    }
}
