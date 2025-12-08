using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using inflan_api.Interfaces;
using inflan_api.DTOs;

namespace inflan_api.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private static readonly Dictionary<int, HashSet<string>> _userConnections = new();
        // Track which users are currently viewing which conversations
        // Key: conversationId, Value: Set of userIds currently in that conversation
        private static readonly Dictionary<int, HashSet<int>> _conversationViewers = new();
        private static readonly object _lock = new();

        public ChatHub(IChatService chatService, IUserService userService, INotificationService notificationService)
        {
            _chatService = chatService;
            _userService = userService;
            _notificationService = notificationService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId > 0)
            {
                // Add connection to user's connection list
                lock (_lock)
                {
                    if (!_userConnections.ContainsKey(userId))
                        _userConnections[userId] = new HashSet<string>();
                    _userConnections[userId].Add(Context.ConnectionId);
                }

                // Add user to their personal group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

                // Notify others that user is online
                await Clients.Others.SendAsync("UserOnline", new OnlineStatusDto
                {
                    UserId = userId,
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow
                });

                Console.WriteLine($"User {userId} connected with connection {Context.ConnectionId}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            if (userId > 0)
            {
                bool isLastConnection = false;

                lock (_lock)
                {
                    if (_userConnections.ContainsKey(userId))
                    {
                        _userConnections[userId].Remove(Context.ConnectionId);
                        if (_userConnections[userId].Count == 0)
                        {
                            _userConnections.Remove(userId);
                            isLastConnection = true;

                            // Clean up conversation viewers for this user when they fully disconnect
                            var conversationsToClean = new List<int>();
                            foreach (var kvp in _conversationViewers)
                            {
                                if (kvp.Value.Contains(userId))
                                {
                                    kvp.Value.Remove(userId);
                                    if (kvp.Value.Count == 0)
                                        conversationsToClean.Add(kvp.Key);
                                }
                            }
                            foreach (var convId in conversationsToClean)
                            {
                                _conversationViewers.Remove(convId);
                            }
                        }
                    }
                }

                // Remove from personal group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");

                // Notify others that user is offline (only if this was their last connection)
                if (isLastConnection)
                {
                    await Clients.Others.SendAsync("UserOffline", new OnlineStatusDto
                    {
                        UserId = userId,
                        IsOnline = false,
                        LastSeen = DateTime.UtcNow
                    });
                }

                Console.WriteLine($"User {userId} disconnected from connection {Context.ConnectionId}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Join a specific conversation room to receive messages
        /// </summary>
        public async Task JoinConversation(int conversationId)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            // Verify user is part of conversation
            var conversation = await _chatService.GetConversationById(conversationId, userId);
            if (conversation == null)
            {
                await Clients.Caller.SendAsync("Error", "You are not authorized to join this conversation");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

            // Track that this user is now viewing this conversation
            lock (_lock)
            {
                if (!_conversationViewers.ContainsKey(conversationId))
                    _conversationViewers[conversationId] = new HashSet<int>();
                _conversationViewers[conversationId].Add(userId);
            }

            Console.WriteLine($"User {userId} joined conversation {conversationId}");

            // Mark messages as read when joining conversation
            await _chatService.MarkAsRead(conversationId, userId);

            // Notify the other user that messages have been read
            var otherUserId = conversation.OtherUserId;
            await Clients.Group($"user_{otherUserId}").SendAsync("MessagesRead", new
            {
                ConversationId = conversationId,
                ReadByUserId = userId,
                ReadAt = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Leave a conversation room
        /// </summary>
        public async Task LeaveConversation(int conversationId)
        {
            var userId = GetUserId();

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

            // Remove user from conversation viewers
            if (userId > 0)
            {
                lock (_lock)
                {
                    if (_conversationViewers.ContainsKey(conversationId))
                    {
                        _conversationViewers[conversationId].Remove(userId);
                        if (_conversationViewers[conversationId].Count == 0)
                            _conversationViewers.Remove(conversationId);
                    }
                }
            }

            Console.WriteLine($"User {userId} left conversation {conversationId}");
        }

        /// <summary>
        /// Send a text message
        /// </summary>
        public async Task SendMessage(int conversationId, string content)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized");
                return;
            }

            var (success, message, messageDto) = await _chatService.SendMessage(userId, conversationId, content);

            if (!success || messageDto == null)
            {
                await Clients.Caller.SendAsync("Error", message);
                return;
            }

            // Send to all users in the conversation
            await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", messageDto);

            // Also send to the recipient's personal group (in case they're not in the conversation view)
            await Clients.Group($"user_{messageDto.RecipientId}").SendAsync("NewMessage", new
            {
                ConversationId = conversationId,
                Message = messageDto
            });

            // Check if recipient is currently viewing this conversation
            bool recipientInConversation = false;
            lock (_lock)
            {
                if (_conversationViewers.ContainsKey(conversationId))
                {
                    recipientInConversation = _conversationViewers[conversationId].Contains(messageDto.RecipientId);
                }
            }

            // Only create notification if recipient is NOT viewing the conversation
            if (!recipientInConversation)
            {
                try
                {
                    var sender = await _userService.GetUserById(userId);
                    var senderName = sender?.UserType == 2 ? (sender?.BrandName ?? sender?.Name) : sender?.Name;
                    var messagePreview = string.IsNullOrEmpty(content) ? "[Attachment]" : content;

                    var notification = await _notificationService.CreateMessageNotificationAsync(
                        messageDto.RecipientId,
                        userId,
                        senderName ?? "Unknown",
                        conversationId,
                        messagePreview
                    );

                    // Send real-time notification to recipient
                    await Clients.Group($"user_{messageDto.RecipientId}").SendAsync("NewNotification", notification);
                    Console.WriteLine($"Notification sent to user {messageDto.RecipientId} - they are not viewing conversation {conversationId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create notification: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Skipping notification for user {messageDto.RecipientId} - they are viewing conversation {conversationId}");
            }

            Console.WriteLine($"Message sent from {userId} in conversation {conversationId}");
        }

        /// <summary>
        /// Notify that user is typing
        /// </summary>
        public async Task StartTyping(int conversationId)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            var user = await _userService.GetUserById(userId);
            var userName = user?.UserType == 2 ? (user?.BrandName ?? user?.Name) : user?.Name;

            await Clients.OthersInGroup($"conversation_{conversationId}").SendAsync("UserTyping", new TypingIndicatorDto
            {
                ConversationId = conversationId,
                UserId = userId,
                UserName = userName,
                IsTyping = true
            });
        }

        /// <summary>
        /// Notify that user stopped typing
        /// </summary>
        public async Task StopTyping(int conversationId)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            await Clients.OthersInGroup($"conversation_{conversationId}").SendAsync("UserTyping", new TypingIndicatorDto
            {
                ConversationId = conversationId,
                UserId = userId,
                IsTyping = false
            });
        }

        /// <summary>
        /// Mark messages as read
        /// </summary>
        public async Task MarkMessagesAsRead(int conversationId)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            var (success, _) = await _chatService.MarkAsRead(conversationId, userId);
            if (success)
            {
                // Notify the other user that messages have been read
                var conversation = await _chatService.GetConversationById(conversationId, userId);
                if (conversation != null)
                {
                    await Clients.Group($"user_{conversation.OtherUserId}").SendAsync("MessagesRead", new
                    {
                        ConversationId = conversationId,
                        ReadByUserId = userId,
                        ReadAt = DateTime.UtcNow
                    });
                }
            }
        }

        /// <summary>
        /// Delete a message
        /// </summary>
        public async Task DeleteMessage(int messageId, bool deleteForEveryone = false)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            var (success, message) = await _chatService.DeleteMessage(messageId, userId, deleteForEveryone);
            if (success)
            {
                // Notify the caller
                await Clients.Caller.SendAsync("MessageDeleted", new
                {
                    MessageId = messageId,
                    DeletedForEveryone = deleteForEveryone
                });

                // If deleted for everyone, notify others in the conversation
                if (deleteForEveryone)
                {
                    // We need to get the conversation ID - for now just broadcast to caller's groups
                    await Clients.Others.SendAsync("MessageDeletedForEveryone", new { MessageId = messageId });
                }
            }
            else
            {
                await Clients.Caller.SendAsync("Error", message);
            }
        }

        /// <summary>
        /// Check if a user is online
        /// </summary>
        public async Task CheckOnlineStatus(int userId)
        {
            bool isOnline;
            lock (_lock)
            {
                isOnline = _userConnections.ContainsKey(userId) && _userConnections[userId].Count > 0;
            }

            await Clients.Caller.SendAsync("OnlineStatus", new OnlineStatusDto
            {
                UserId = userId,
                IsOnline = isOnline
            });
        }

        /// <summary>
        /// Get list of online users from a list of user IDs
        /// </summary>
        public async Task GetOnlineUsers(int[] userIds)
        {
            var onlineStatuses = new List<OnlineStatusDto>();

            lock (_lock)
            {
                foreach (var userId in userIds)
                {
                    onlineStatuses.Add(new OnlineStatusDto
                    {
                        UserId = userId,
                        IsOnline = _userConnections.ContainsKey(userId) && _userConnections[userId].Count > 0
                    });
                }
            }

            await Clients.Caller.SendAsync("OnlineUsersList", onlineStatuses);
        }

        private int GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return 0;
            return userId;
        }

        /// <summary>
        /// Check if a user is currently viewing a specific conversation
        /// This is used by the REST API to determine if notifications should be sent
        /// </summary>
        public static bool IsUserViewingConversation(int userId, int conversationId)
        {
            lock (_lock)
            {
                if (_conversationViewers.ContainsKey(conversationId))
                {
                    return _conversationViewers[conversationId].Contains(userId);
                }
            }
            return false;
        }

        /// <summary>
        /// Check if a user is connected to SignalR
        /// </summary>
        public static bool IsUserConnected(int userId)
        {
            lock (_lock)
            {
                return _userConnections.ContainsKey(userId) && _userConnections[userId].Count > 0;
            }
        }
    }
}
