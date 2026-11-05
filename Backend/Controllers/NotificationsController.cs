using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.DTOs;
using ServerCSharp.Models;

namespace ServerCSharp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Notifications - Thông Báo")]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotificationsController(AppDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : null;
        }

        // GET: api/notifications (Lấy danh sách thông báo của người dùng hiện tại)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] int limit = 20)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var list = await _context.Notifications
                .Where(n => n.UserId == userId.Value)
                .OrderByDescending(n => n.CreatedAt)
                .Take(Math.Min(limit, 50))
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    UserId = n.UserId,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type,
                    ActionUrl = n.ActionUrl,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId.Value && !n.IsRead);

            return Ok(new
            {
                unreadCount,
                notifications = list
            });
        }

        // GET: api/notifications/unread-count (Đếm số thông báo chưa đọc)
        [Authorize]
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId.Value && !n.IsRead);

            return Ok(new { unreadCount });
        }

        // PUT: api/notifications/{id}/read (Đánh dấu 1 thông báo là đã đọc)
        [Authorize]
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Value);
            if (notif == null)
            {
                return NotFound(new { message = "Không tìm thấy thông báo" });
            }

            notif.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã đánh dấu thông báo là đã đọc", id = notif.Id });
        }

        // PUT: api/notifications/read-all (Đánh dấu toàn bộ thông báo là đã đọc)
        [Authorize]
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var unreadList = await _context.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead)
                .ToListAsync();

            foreach (var n in unreadList)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã đánh dấu tất cả thông báo là đã đọc", updatedCount = unreadList.Count });
        }

        // POST: api/notifications (Tạo thông báo mới - dành cho Admin hoặc kích hoạt hệ thống)
        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Tiêu đề và nội dung thông báo không được để trống" });
            }

            var targetUser = await _context.Users.FindAsync(request.UserId);
            if (targetUser == null)
            {
                return NotFound(new { message = "Người nhận không tồn tại" });
            }

            var notification = new Notification
            {
                UserId = request.UserId,
                Title = request.Title.Trim(),
                Message = request.Message.Trim(),
                Type = string.IsNullOrWhiteSpace(request.Type) ? "system" : request.Type.Trim(),
                ActionUrl = request.ActionUrl?.Trim(),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Đã gửi thông báo thành công",
                notification = new NotificationDto
                {
                    Id = notification.Id,
                    UserId = notification.UserId,
                    Title = notification.Title,
                    Message = notification.Message,
                    Type = notification.Type,
                    ActionUrl = notification.ActionUrl,
                    IsRead = notification.IsRead,
                    CreatedAt = notification.CreatedAt
                }
            });
        }
    }
}
