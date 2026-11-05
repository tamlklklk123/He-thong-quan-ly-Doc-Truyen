using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;

namespace ServerCSharp.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class NotificationController : Controller
    {
        private readonly AppDbContext _context;

        public NotificationController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Notification/GetMyNotifications
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            int? userId = null;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(idClaim, out int uid))
                {
                    userId = uid;
                }
            }

            if (!userId.HasValue) return Unauthorized();

            var list = await _context.Notifications
                .Where(n => !userId.HasValue || n.UserId == userId.Value)
                .OrderByDescending(n => n.CreatedAt)
                .Take(15)
                .Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    type = n.Type,
                    actionUrl = n.ActionUrl ?? "/",
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt,
                    timeAgo = GetTimeAgo(n.CreatedAt)
                })
                .ToListAsync();

            var unreadCount = list.Count(n => !n.isRead);

            return Json(new { success = true, unreadCount, notifications = list });
        }

        // POST: /Notification/MarkRead/{id}
        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            var notif = await _context.Notifications.FindAsync(id);
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (notif != null && notif.UserId == uid)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        // POST: /Notification/MarkAllRead
        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            int? userId = null;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(idClaim, out int uid)) userId = uid;
            }

            if (!userId.HasValue) return Unauthorized();
            var unread = await _context.Notifications
                .Where(n => (!userId.HasValue || n.UserId == userId.Value) && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private static string GetTimeAgo(DateTime dt)
        {
            var span = DateTime.UtcNow - dt;
            if (span.TotalMinutes < 1) return "Vừa xong";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
            return $"{(int)span.TotalDays} ngày trước";
        }
    }
}
