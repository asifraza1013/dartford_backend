using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.DTOs;

namespace inflan_api.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepository;
        private readonly IUserService _userService;
        private readonly ICampaignService _campaignService;
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly string[] AllowedFileExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip" };
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

        public ChatService(
            IChatRepository chatRepository,
            IUserService userService,
            ICampaignService campaignService,
            IWebHostEnvironment environment)
        {
            _chatRepository = chatRepository;
            _userService = userService;
            _campaignService = campaignService;
            _environment = environment;
        }

        #region Conversation Methods

        public async Task<(bool Success, string Message, Conversation? Conversation)> GetOrCreateConversation(int userId, int otherUserId)
        {
            // Get both users to determine their types
            var user = await _userService.GetUserById(userId);
            var otherUser = await _userService.GetUserById(otherUserId);

            if (user == null || otherUser == null)
                return (false, "One or both users not found", null);

            // Determine which is brand and which is influencer
            int brandId, influencerId;

            if (user.UserType == 2 && otherUser.UserType == 3)
            {
                // User is brand, other is influencer
                brandId = userId;
                influencerId = otherUserId;
            }
            else if (user.UserType == 3 && otherUser.UserType == 2)
            {
                // User is influencer, other is brand
                brandId = otherUserId;
                influencerId = userId;
            }
            else
            {
                return (false, "Conversations can only be between a brand and an influencer", null);
            }

            // Check if conversation already exists
            var existingConversation = await _chatRepository.GetConversationByParticipants(brandId, influencerId);

            if (existingConversation != null)
            {
                // Restore conversation if it was deleted by current user
                if (userId == brandId && existingConversation.IsDeletedByBrand)
                {
                    existingConversation.IsDeletedByBrand = false;
                    await _chatRepository.UpdateConversation(existingConversation);
                }
                else if (userId == influencerId && existingConversation.IsDeletedByInfluencer)
                {
                    existingConversation.IsDeletedByInfluencer = false;
                    await _chatRepository.UpdateConversation(existingConversation);
                }

                return (true, "Conversation found", existingConversation);
            }

            // Create new conversation
            var conversation = new Conversation
            {
                BrandId = brandId,
                InfluencerId = influencerId,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _chatRepository.CreateConversation(conversation);
            return (true, "Conversation created", created);
        }

        public async Task<ConversationDto?> GetConversationById(int conversationId, int userId)
        {
            var conversation = await _chatRepository.GetConversationById(conversationId);
            if (conversation == null) return null;

            // Check if user is part of conversation
            if (conversation.BrandId != userId && conversation.InfluencerId != userId)
                return null;

            // Check if deleted for this user
            if ((conversation.BrandId == userId && conversation.IsDeletedByBrand) ||
                (conversation.InfluencerId == userId && conversation.IsDeletedByInfluencer))
                return null;

            return await EnrichConversation(conversation, userId);
        }

        public async Task<IEnumerable<ConversationDto>> GetUserConversations(int userId)
        {
            var conversations = await _chatRepository.GetConversationsByUserId(userId);
            var enrichedConversations = new List<ConversationDto>();

            foreach (var conversation in conversations)
            {
                var enriched = await EnrichConversation(conversation, userId);
                if (enriched != null)
                    enrichedConversations.Add(enriched);
            }

            return enrichedConversations;
        }

        public async Task<(bool Success, string Message)> DeleteConversation(int conversationId, int userId)
        {
            var conversation = await _chatRepository.GetConversationById(conversationId);
            if (conversation == null)
                return (false, "Conversation not found");

            if (conversation.BrandId != userId && conversation.InfluencerId != userId)
                return (false, "You are not authorized to delete this conversation");

            var deleted = await _chatRepository.DeleteConversationForUser(conversationId, userId);
            if (!deleted)
                return (false, "Failed to delete conversation");

            return (true, "Conversation deleted successfully");
        }

        private async Task<ConversationDto?> EnrichConversation(Conversation conversation, int userId)
        {
            var brandUser = await _userService.GetUserById(conversation.BrandId);
            var influencerUser = await _userService.GetUserById(conversation.InfluencerId);

            if (brandUser == null || influencerUser == null)
                return null;

            // Determine the other user
            bool isBrand = conversation.BrandId == userId;
            var otherUser = isBrand ? influencerUser : brandUser;

            // Get last message
            var messages = await _chatRepository.GetMessagesByConversationId(conversation.Id, userId, 1, 1);
            var lastMessage = messages.FirstOrDefault();
            MessageDto? lastMessageDto = null;

            if (lastMessage != null)
            {
                lastMessageDto = await EnrichMessage(lastMessage, userId);
            }

            // Get unread count
            var unreadCount = await _chatRepository.GetUnreadMessageCount(conversation.Id, userId);

            // Get campaign name if linked
            string? campaignName = null;
            if (conversation.CampaignId.HasValue)
            {
                var campaign = await _campaignService.GetCampaignById(conversation.CampaignId.Value);
                campaignName = campaign?.ProjectName;
            }

            return new ConversationDto
            {
                Id = conversation.Id,
                BrandId = conversation.BrandId,
                BrandName = brandUser.BrandName ?? brandUser.Name,
                BrandProfileImage = brandUser.ProfileImage,
                InfluencerId = conversation.InfluencerId,
                InfluencerName = influencerUser.Name,
                InfluencerProfileImage = influencerUser.ProfileImage,
                CampaignId = conversation.CampaignId,
                CampaignName = campaignName,
                CreatedAt = conversation.CreatedAt,
                LastMessageAt = conversation.LastMessageAt,
                LastMessage = lastMessageDto,
                UnreadCount = unreadCount,
                OtherUserId = otherUser.Id,
                OtherUserName = otherUser.UserType == 2 ? (otherUser.BrandName ?? otherUser.Name) : otherUser.Name,
                OtherUserProfileImage = otherUser.ProfileImage
            };
        }

        #endregion

        #region Message Methods

        public async Task<(bool Success, string Message, MessageDto? MessageDto)> SendMessage(int senderId, int conversationId, string? content, int messageType = 1)
        {
            var conversation = await _chatRepository.GetConversationById(conversationId);
            if (conversation == null)
                return (false, "Conversation not found", null);

            // Check if sender is part of conversation
            if (conversation.BrandId != senderId && conversation.InfluencerId != senderId)
                return (false, "You are not authorized to send messages in this conversation", null);

            if (string.IsNullOrWhiteSpace(content) && messageType == ChatMessageType.Text)
                return (false, "Message content cannot be empty", null);

            // Determine recipient
            int recipientId = conversation.BrandId == senderId ? conversation.InfluencerId : conversation.BrandId;

            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderId = senderId,
                RecipientId = recipientId,
                Content = content,
                MessageType = messageType,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _chatRepository.CreateMessage(message);
            var messageDto = await EnrichMessage(created, senderId);

            return (true, "Message sent", messageDto);
        }

        public async Task<(bool Success, string Message, MessageDto? MessageDto)> SendMessageWithAttachment(int senderId, int conversationId, string? content, IFormFile file)
        {
            var conversation = await _chatRepository.GetConversationById(conversationId);
            if (conversation == null)
                return (false, "Conversation not found", null);

            // Check if sender is part of conversation
            if (conversation.BrandId != senderId && conversation.InfluencerId != senderId)
                return (false, "You are not authorized to send messages in this conversation", null);

            // Validate and save file
            var attachmentUrl = await SaveChatAttachment(file);
            if (attachmentUrl == null)
                return (false, "Failed to upload attachment. Please check file type and size.", null);

            // Determine message type based on file
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            int messageType = AllowedImageExtensions.Contains(extension) ? ChatMessageType.Image : ChatMessageType.File;

            // Determine recipient
            int recipientId = conversation.BrandId == senderId ? conversation.InfluencerId : conversation.BrandId;

            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderId = senderId,
                RecipientId = recipientId,
                Content = content,
                MessageType = messageType,
                AttachmentUrl = attachmentUrl,
                AttachmentName = file.FileName,
                AttachmentSize = file.Length,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _chatRepository.CreateMessage(message);
            var messageDto = await EnrichMessage(created, senderId);

            return (true, "Message sent", messageDto);
        }

        public async Task<IEnumerable<MessageDto>> GetMessages(int conversationId, int userId, int page = 1, int pageSize = 50)
        {
            var conversation = await _chatRepository.GetConversationById(conversationId);
            if (conversation == null)
                return Enumerable.Empty<MessageDto>();

            // Check if user is part of conversation
            if (conversation.BrandId != userId && conversation.InfluencerId != userId)
                return Enumerable.Empty<MessageDto>();

            var messages = await _chatRepository.GetMessagesByConversationId(conversationId, userId, page, pageSize);
            var messageDtos = new List<MessageDto>();

            foreach (var message in messages)
            {
                var dto = await EnrichMessage(message, userId);
                if (dto != null)
                    messageDtos.Add(dto);
            }

            return messageDtos;
        }

        public async Task<int> GetUnreadCount(int userId)
        {
            return await _chatRepository.GetTotalUnreadMessageCount(userId);
        }

        public async Task<(bool Success, string Message)> MarkAsRead(int conversationId, int userId)
        {
            var conversation = await _chatRepository.GetConversationById(conversationId);
            if (conversation == null)
                return (false, "Conversation not found");

            if (conversation.BrandId != userId && conversation.InfluencerId != userId)
                return (false, "You are not authorized to access this conversation");

            await _chatRepository.MarkMessagesAsRead(conversationId, userId);
            return (true, "Messages marked as read");
        }

        public async Task<(bool Success, string Message)> DeleteMessage(int messageId, int userId, bool deleteForEveryone = false)
        {
            var message = await _chatRepository.GetMessageById(messageId);
            if (message == null)
                return (false, "Message not found");

            // Check if user is part of this message
            if (message.SenderId != userId && message.RecipientId != userId)
                return (false, "You are not authorized to delete this message");

            bool success;
            if (deleteForEveryone && message.SenderId == userId)
            {
                // Only sender can delete for everyone
                success = await _chatRepository.DeleteMessageForEveryone(messageId, userId);
            }
            else
            {
                success = await _chatRepository.DeleteMessageForUser(messageId, userId);
            }

            if (!success)
                return (false, "Failed to delete message");

            return (true, "Message deleted successfully");
        }

        private async Task<MessageDto?> EnrichMessage(ChatMessage message, int userId)
        {
            var sender = await _userService.GetUserById(message.SenderId);
            if (sender == null) return null;

            return new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = sender.UserType == 2 ? (sender.BrandName ?? sender.Name) : sender.Name,
                SenderProfileImage = sender.ProfileImage,
                RecipientId = message.RecipientId,
                Content = message.Content,
                MessageType = message.MessageType,
                AttachmentUrl = message.AttachmentUrl,
                AttachmentName = message.AttachmentName,
                AttachmentSize = message.AttachmentSize,
                CreatedAt = message.CreatedAt,
                IsRead = message.IsRead,
                ReadAt = message.ReadAt,
                IsMine = message.SenderId == userId
            };
        }

        #endregion

        #region File Handling

        public async Task<string?> SaveChatAttachment(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            // Check file size
            if (file.Length > MaxFileSize)
                return null;

            // Check file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allAllowedExtensions = AllowedImageExtensions.Concat(AllowedFileExtensions).ToArray();

            if (!allAllowedExtensions.Contains(extension))
                return null;

            // Create upload directory
            var uploadDir = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "chat");
            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative URL
            return $"/uploads/chat/{fileName}";
        }

        #endregion
    }
}
