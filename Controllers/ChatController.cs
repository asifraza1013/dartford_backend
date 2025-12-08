using System.Security.Claims;
using inflan_api.Interfaces;
using inflan_api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using inflan_api.Hubs;

namespace inflan_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;

        public ChatController(
            IChatService chatService,
            IHubContext<ChatHub> hubContext,
            INotificationService notificationService,
            IUserService userService)
        {
            _chatService = chatService;
            _hubContext = hubContext;
            _notificationService = notificationService;
            _userService = userService;
        }

        /// <summary>
        /// Get all conversations for the current user
        /// </summary>
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            var conversations = await _chatService.GetUserConversations(userId);
            return Ok(conversations);
        }

        /// <summary>
        /// Get a specific conversation by ID
        /// </summary>
        [HttpGet("conversations/{conversationId}")]
        public async Task<IActionResult> GetConversation(int conversationId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            var conversation = await _chatService.GetConversationById(conversationId, userId);
            if (conversation == null)
                return NotFound(new { message = "Conversation not found", code = "CONVERSATION_NOT_FOUND" });

            return Ok(conversation);
        }

        /// <summary>
        /// Start a new conversation or get existing one with another user
        /// </summary>
        [HttpPost("conversations/start")]
        public async Task<IActionResult> StartConversation([FromBody] StartConversationRequest request)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            var (success, message, conversation) = await _chatService.GetOrCreateConversation(userId, request.OtherUserId);

            if (!success || conversation == null)
                return BadRequest(new { message, code = "CONVERSATION_CREATION_FAILED" });

            // If initial message provided, send it
            if (!string.IsNullOrWhiteSpace(request.InitialMessage))
            {
                var (msgSuccess, msgMessage, messageDto) = await _chatService.SendMessage(userId, conversation.Id, request.InitialMessage);

                if (msgSuccess && messageDto != null)
                {
                    // Notify via SignalR
                    await _hubContext.Clients.Group($"user_{messageDto.RecipientId}").SendAsync("NewMessage", new
                    {
                        ConversationId = conversation.Id,
                        Message = messageDto
                    });
                }
            }

            // Return enriched conversation
            var enrichedConversation = await _chatService.GetConversationById(conversation.Id, userId);
            return Ok(new { message, conversation = enrichedConversation });
        }

        /// <summary>
        /// Delete a conversation (soft delete for current user only)
        /// </summary>
        [HttpDelete("conversations/{conversationId}")]
        public async Task<IActionResult> DeleteConversation(int conversationId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            var (success, message) = await _chatService.DeleteConversation(conversationId, userId);

            if (!success)
                return BadRequest(new { message, code = "DELETE_FAILED" });

            return Ok(new { message, code = "CONVERSATION_DELETED" });
        }

        /// <summary>
        /// Get messages for a conversation with pagination
        /// </summary>
        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<IActionResult> GetMessages(int conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            var messages = await _chatService.GetMessages(conversationId, userId, page, pageSize);
            return Ok(messages);
        }

        /// <summary>
        /// Send a text message
        /// </summary>
        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            var (success, message, messageDto) = await _chatService.SendMessage(
                userId,
                request.ConversationId,
                request.Content,
                request.MessageType
            );

            if (!success || messageDto == null)
                return BadRequest(new { message, code = "MESSAGE_SEND_FAILED" });

            // Notify via SignalR
            await _hubContext.Clients.Group($"conversation_{request.ConversationId}").SendAsync("ReceiveMessage", messageDto);
            await _hubContext.Clients.Group($"user_{messageDto.RecipientId}").SendAsync("NewMessage", new
            {
                ConversationId = request.ConversationId,
                Message = messageDto
            });

            // Create notification for recipient
            await CreateMessageNotification(userId, messageDto.RecipientId, request.ConversationId, request.Content ?? "");

            return Ok(new { message = "Message sent", data = messageDto });
        }

        /// <summary>
        /// Send a message with file attachment
        /// </summary>
        [HttpPost("messages/attachment")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SendMessageWithAttachment(
            [FromForm] int conversationId,
            [FromForm] string? content,
            IFormFile file)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided", code = "NO_FILE" });

            var (success, message, messageDto) = await _chatService.SendMessageWithAttachment(userId, conversationId, content, file);

            if (!success || messageDto == null)
                return BadRequest(new { message, code = "MESSAGE_SEND_FAILED" });

            // Notify via SignalR
            await _hubContext.Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", messageDto);
            await _hubContext.Clients.Group($"user_{messageDto.RecipientId}").SendAsync("NewMessage", new
            {
                ConversationId = conversationId,
                Message = messageDto
            });

            // Create notification for recipient
            await CreateMessageNotification(userId, messageDto.RecipientId, conversationId, content ?? "");

            return Ok(new { message = "Message sent", data = messageDto });
        }

        /// <summary>
        /// Mark all messages in a conversation as read
        /// </summary>
        [HttpPost("conversations/{conversationId}/read")]
        public async Task<IActionResult> MarkAsRead(int conversationId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            var (success, message) = await _chatService.MarkAsRead(conversationId, userId);

            if (!success)
                return BadRequest(new { message, code = "MARK_READ_FAILED" });

            // Get conversation to notify the other user
            var conversation = await _chatService.GetConversationById(conversationId, userId);
            if (conversation != null)
            {
                await _hubContext.Clients.Group($"user_{conversation.OtherUserId}").SendAsync("MessagesRead", new
                {
                    ConversationId = conversationId,
                    ReadByUserId = userId,
                    ReadAt = DateTime.UtcNow
                });
            }

            return Ok(new { message, code = "MESSAGES_READ" });
        }

        /// <summary>
        /// Delete a message
        /// </summary>
        [HttpDelete("messages/{messageId}")]
        public async Task<IActionResult> DeleteMessage(int messageId, [FromQuery] bool deleteForEveryone = false)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            var (success, message) = await _chatService.DeleteMessage(messageId, userId, deleteForEveryone);

            if (!success)
                return BadRequest(new { message, code = "DELETE_FAILED" });

            // Notify via SignalR if deleted for everyone
            if (deleteForEveryone)
            {
                await _hubContext.Clients.All.SendAsync("MessageDeletedForEveryone", new { MessageId = messageId });
            }

            return Ok(new { message, code = "MESSAGE_DELETED" });
        }

        /// <summary>
        /// Get total unread message count for current user
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Please login again", code = "INVALID_TOKEN" });

            var count = await _chatService.GetUnreadCount(userId);
            return Ok(new { unreadCount = count });
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return 0;
            return userId;
        }

        private async Task CreateMessageNotification(int senderId, int recipientId, int conversationId, string messageContent)
        {
            try
            {
                // Get sender name
                var sender = await _userService.GetUserById(senderId);
                var senderName = sender?.Name ?? "Someone";

                // Create preview (truncate long messages)
                var messagePreview = string.IsNullOrEmpty(messageContent)
                    ? "Sent an attachment"
                    : messageContent.Length > 50
                        ? messageContent.Substring(0, 47) + "..."
                        : messageContent;

                // Create notification
                var notification = await _notificationService.CreateMessageNotificationAsync(
                    recipientId,
                    senderId,
                    senderName,
                    conversationId,
                    messagePreview
                );

                // Send real-time notification via SignalR
                if (notification != null)
                {
                    await _hubContext.Clients.Group($"user_{recipientId}").SendAsync("NewNotification", notification);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the message send
                Console.WriteLine($"Failed to create notification: {ex.Message}");
            }
        }
    }
}
