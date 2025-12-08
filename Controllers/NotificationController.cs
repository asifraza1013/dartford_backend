using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using inflan_api.DTOs;
using inflan_api.Interfaces;

namespace inflan_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get notifications for the current user
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var result = await _notificationService.GetNotificationsAsync(userId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { unreadCount = count });
        }

        /// <summary>
        /// Mark a single notification as read
        /// </summary>
        [HttpPost("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var success = await _notificationService.MarkAsReadAsync(notificationId, userId);
            if (!success)
            {
                return NotFound(new { message = "Notification not found", code = "NOT_FOUND" });
            }

            return Ok(new { message = "Notification marked as read", code = "SUCCESS" });
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new { message = "All notifications marked as read", code = "SUCCESS" });
        }

        /// <summary>
        /// Mark multiple notifications as read
        /// </summary>
        [HttpPost("read-multiple")]
        public async Task<IActionResult> MarkMultipleAsRead([FromBody] MarkNotificationsReadRequest request)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            if (request.NotificationIds == null || !request.NotificationIds.Any())
            {
                // If no IDs provided, mark all as read
                await _notificationService.MarkAllAsReadAsync(userId);
            }
            else
            {
                await _notificationService.MarkMultipleAsReadAsync(request.NotificationIds, userId);
            }

            return Ok(new { message = "Notifications marked as read", code = "SUCCESS" });
        }

        /// <summary>
        /// Delete a notification
        /// </summary>
        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(int notificationId)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var success = await _notificationService.DeleteNotificationAsync(notificationId, userId);
            if (!success)
            {
                return NotFound(new { message = "Notification not found", code = "NOT_FOUND" });
            }

            return Ok(new { message = "Notification deleted", code = "SUCCESS" });
        }

        /// <summary>
        /// Delete all notifications
        /// </summary>
        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAllNotifications()
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            await _notificationService.DeleteAllNotificationsAsync(userId);
            return Ok(new { message = "All notifications deleted", code = "SUCCESS" });
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return 0;
            return userId;
        }
    }
}
