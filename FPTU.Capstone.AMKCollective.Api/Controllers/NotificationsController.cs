using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class NotificationsController : BaseApiController
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Retrieves the paginated notifications for the authenticated user.
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = "Get User Notifications", Description = "Retrieves a paginated list for the currently logged-in user.")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] string? referenceType, [FromQuery] string? type, [FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, referenceType, type, cursor, pageSize, cancellationToken);
            return SuccessResponse(notifications);
        }

        [HttpGet("unread-count")]
        [SwaggerOperation(Summary = "Get Unread Notifications Count")]
        public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            int count = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
            return SuccessResponse(new { UnreadCount = count });
        }

        [HttpPost("read-all")]
        [SwaggerOperation(Summary = "Mark All Notifications as Read")]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            await _notificationService.MarkAllAsReadAsync(userId);
            return SuccessResponse(new { }, "All notifications marked as read");
        }

        [HttpPost("{id}/read")]
        [SwaggerOperation(Summary = "Mark Notification as Read")]
        public async Task<IActionResult> MarkNotificationRead(int id, CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            await _notificationService.MarkAsReadSecureAsync(id, userId, cancellationToken);
            return SuccessResponse(new { NotificationId = id }, "Notification marked as read successfully");
        }

        /// <summary>
        /// Retrieves paginated system notifications for Admin.
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        [SwaggerOperation(Summary = "Get All System Notifications", Description = "Admin endpoint to retrieve all notifications in the system.")]
        [SwaggerResponse(200, "Successfully retrieved notifications")]
        public async Task<IActionResult> GetAllNotifications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var (items, totalCount) = await _notificationService.GetAllSystemNotificationsPagedAsync(pageNumber, pageSize, cancellationToken);
            return SuccessResponse(new { Items = items, TotalCount = totalCount });
        }

        /// <summary>
        /// Retrieves a specific notification by ID (Admin).
        /// </summary>
        [HttpGet("admin/{id}")]
        [Authorize(Roles = "Admin")]
        [SwaggerOperation(Summary = "Get Notification By ID", Description = "Admin endpoint to retrieve a specific notification.")]
        [SwaggerResponse(200, "Successfully retrieved notification", typeof(NotificationDto))]
        [SwaggerResponse(404, "Notification not found")]
        public async Task<IActionResult> GetNotificationById(int id, CancellationToken cancellationToken = default)
        {
            var notification = await _notificationService.GetNotificationByIdAsync(id, cancellationToken);
            return SuccessResponse(notification);
        }

        /// <summary>
        /// Creates a new system notification (Admin).
        /// </summary>
        [HttpPost("admin")]
        [Authorize(Roles = "Admin")]
        [SwaggerOperation(Summary = "Create System Notification", Description = "Admin endpoint to dispatch a direct system notification to a user.")]
        [SwaggerResponse(200, "Notification created successfully", typeof(NotificationDto))]
        public async Task<IActionResult> CreateNotification([FromBody] CreateSystemNotificationDto request, CancellationToken cancellationToken = default)
        {
            var notification = await _notificationService.CreateSystemNotificationAsync(request, cancellationToken);
            return SuccessResponse(notification, "Notification created successfully");
        }

        /// <summary>
        /// Updates a notification (Admin).
        /// </summary>
        [HttpPut("admin/{id}")]
        [Authorize(Roles = "Admin")]
        [SwaggerOperation(Summary = "Update Notification", Description = "Admin endpoint to override notification details.")]
        [SwaggerResponse(200, "Notification updated successfully", typeof(NotificationDto))]
        [SwaggerResponse(404, "Notification not found")]
        public async Task<IActionResult> UpdateNotification(int id, [FromBody] UpdateNotificationDto request, CancellationToken cancellationToken = default)
        {
            var notification = await _notificationService.UpdateNotificationAsync(id, request, cancellationToken);
            return SuccessResponse(notification, "Notification updated successfully");
        }

        /// <summary>
        /// Deletes a notification (Admin).
        /// </summary>
        [HttpDelete("admin/{id}")]
        [Authorize(Roles = "Admin")]
        [SwaggerOperation(Summary = "Delete Notification", Description = "Admin endpoint to hard-delete a notification from the system.")]
        [SwaggerResponse(200, "Notification deleted successfully")]
        [SwaggerResponse(404, "Notification not found")]
        public async Task<IActionResult> DeleteNotification(int id, CancellationToken cancellationToken = default)
        {
            await _notificationService.DeleteNotificationAsync(id, cancellationToken);
            return SuccessResponse(new { Id = id }, "Notification deleted successfully");
        }

        /// <summary>
        /// Broadcasts a system notification to all users (Admin).
        /// </summary>
        [HttpPost("admin/broadcast")]
        [Authorize(Roles = "Admin")]
        [SwaggerOperation(Summary = "Broadcast System Notification", Description = "Admin endpoint to send a system notification to all users.")]
        [SwaggerResponse(200, "Broadcast sent successfully")]
        public async Task<IActionResult> BroadcastNotification([FromBody] BroadcastNotificationDto request, CancellationToken cancellationToken = default)
        {
            await _notificationService.CreateBroadcastSystemNotificationsAsync(request, cancellationToken);
            return SuccessResponse(new { }, "Broadcast sent successfully");
        }
    }
}
