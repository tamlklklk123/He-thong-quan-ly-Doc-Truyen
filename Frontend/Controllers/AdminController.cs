using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Hubs;
using ServerCSharp.Models;
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<RealtimeHub> _hubContext;

        public AdminController(AppDbContext context, IHubContext<RealtimeHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index(string? tab = null, string? search = null, string? filter = null, string? memberSearch = null)
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalStories = await _context.Stories.CountAsync();
            var totalChapters = await _context.Chapters.CountAsync();
            var pendingStoriesCount = await _context.Stories.CountAsync(s => s.ApprovalStatus == "pending");
            var pendingReportsCount = await _context.Reports.CountAsync(r => r.Status == "pending");
            var totalViews = await _context.Stories.SumAsync(s => s.Views);

            var pendingStories = await _context.Stories
                .Include(s => s.Author)
                .Include(s => s.Chapters)
                .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                .Where(s => s.ApprovalStatus == "pending")
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var reports = await _context.Reports
                .Include(r => r.User).OrderByDescending(r => r.Status == "pending")
                .ThenByDescending(r => r.CreatedAt).Take(150).ToListAsync();

            var commentQuery = _context.Comments.Include(c => c.User).Include(c => c.Story).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                commentQuery = commentQuery.Where(c => c.Content.Contains(term) ||
                    (c.User != null && (c.User.Name.Contains(term) || c.User.Email.Contains(term))));
            }
            if (filter == "hidden") commentQuery = commentQuery.Where(c => c.IsHidden);
            if (filter == "visible") commentQuery = commentQuery.Where(c => !c.IsHidden);
            if (filter == "reported")
            {
                var targets = _context.Reports.Where(r => r.TargetType == "comment" && r.Status == "pending")
                    .Select(r => r.TargetId);
                commentQuery = commentQuery.Where(c => targets.Contains(c.Id));
            }
            var comments = await commentQuery.OrderByDescending(c => c.CreatedAt).Take(100).ToListAsync();
            var memberQuery = _context.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(memberSearch))
            {
                var term = memberSearch.Trim();
                memberQuery = memberQuery.Where(u => u.Name.Contains(term) || u.Email.Contains(term));
            }
            var users = await memberQuery.OrderByDescending(u => u.CreatedAt).Take(200).ToListAsync();
            ViewBag.Comments = comments;
            ViewBag.CommentTotal = await _context.Comments.CountAsync();
            ViewBag.HiddenCommentCount = await _context.Comments.CountAsync(c => c.IsHidden);
            ViewBag.CommentSearch = search ?? "";
            ViewBag.CommentFilter = filter ?? "all";
            ViewBag.MemberSearch = memberSearch ?? "";
            ViewBag.ActiveTab = new[] {"pendingTab", "commentsTab", "reportsTab", "usersTab", "genresTab"}.Contains(tab) ? tab : "pendingTab";
            ViewBag.RecentActions = await _context.ModerationLogs.OrderByDescending(m => m.CreatedAt).Take(30).ToListAsync();
            var genres = await _context.Genres.Include(g => g.StoryGenres).ToListAsync();

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalStories = totalStories;
            ViewBag.TotalChapters = totalChapters;
            ViewBag.PendingStoriesCount = pendingStoriesCount;
            ViewBag.PendingReportsCount = pendingReportsCount;
            ViewBag.TotalViews = totalViews;

            ViewBag.PendingStories = pendingStories;
            ViewBag.Reports = reports;
            var reportedCommentIds = reports.Where(r => r.TargetType == "comment").Select(r => r.TargetId).Distinct().ToList();
            var reportedComments = await _context.Comments.Include(c => c.User)
                .Where(c => reportedCommentIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
            ViewBag.ReportedComments = reportedComments;
            ViewBag.Users = users;
            var now = DateTime.UtcNow;
            var memberIds = users.Select(u => u.Id).ToList();
            var commentCounts = await _context.Comments.Where(c => memberIds.Contains(c.UserId))
                .GroupBy(c => c.UserId).Select(g => new { UserId = g.Key, Count = g.Count() }).ToListAsync();
            var reportCounts = await _context.Reports.Where(r => memberIds.Contains(r.UserId))
                .GroupBy(r => r.UserId).Select(g => new { UserId = g.Key, Count = g.Count() }).ToListAsync();
            var completedPayments = await _context.PaymentTransactions
                .Where(t => memberIds.Contains(t.UserId) && t.Status == "paid")
                .GroupBy(t => t.UserId).Select(g => new { UserId = g.Key, Count = g.Count() }).ToListAsync();
            var validSubscribers = await _context.UserSubscriptions
                .Where(t => memberIds.Contains(t.UserId) && t.StartsAt <= now && t.ExpiresAt > now)
                .Select(t => t.UserId).Distinct().ToListAsync();
            var coinDepositors = await _context.PaymentTransactions
                .Where(t => memberIds.Contains(t.UserId) && t.Type == "coins" && t.Status == "paid" && t.CoinAmount > 0)
                .Select(t => t.UserId).Distinct().ToListAsync();
            ViewBag.MemberCommentCounts = commentCounts.ToDictionary(x => x.UserId, x => x.Count);
            ViewBag.MemberReportCounts = reportCounts.ToDictionary(x => x.UserId, x => x.Count);
            ViewBag.MemberPaymentCounts = completedPayments.ToDictionary(x => x.UserId, x => x.Count);
            ViewBag.MemberVipIds = validSubscribers.Intersect(coinDepositors).ToHashSet();
            ViewBag.Genres = genres;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStory(int id)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story != null)
            {
                story.ApprovalStatus = "approved";
                story.RejectionReason = null;
                await _context.SaveChangesAsync();
                var readers = await _context.Users.Where(u => u.Role != "admin").Select(u => u.Id).ToListAsync();
                foreach (var readerId in readers) _context.Notifications.Add(new Notification
                { UserId = readerId, Type = "story", Title = "📚 Truyện mới: " + story.Title,
                  Message = "Truyện mới đã được duyệt và phát hành. Bấm để xem chi tiết!", ActionUrl = $"/Story/Detail/{story.Id}" });
                await _context.SaveChangesAsync();

                // Realtime Notification to all connected users
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    title = "Truyện mới xuất bản! 🎉",
                    message = $"Tác phẩm '{story.Title}' vừa được Ban Quản Trị phê duyệt xuất bản chính thức!",
                    link = $"/Story/Detail/{story.Id}"
                });

                TempData["SuccessMessage"] = $"Truyện \"{story.Title}\" đã được phê duyệt và xuất bản thành công!";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectStory(int id, string reason)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story != null)
            {
                story.ApprovalStatus = "rejected";
                story.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Nội dung chưa đạt tiêu chuẩn kiểm duyệt." : reason.Trim();
                await _context.SaveChangesAsync();
                TempData["InfoMessage"] = $"Đã từ chối truyện \"{story.Title}\" kèm lý do phản hồi cho tác giả.";
            }
            return RedirectToAction("Index");
        }

        private int CurrentAdminId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : 0;

        private void LogAction(string type, int targetId, string action, string reason)
        {
            _context.ModerationLogs.Add(new ModerationLog
            {
                AdminId = CurrentAdminId, TargetType = type, TargetId = targetId,
                Action = action, Reason = reason.Length > 500 ? reason.Substring(0, 500) : reason
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModerateComment(int id, string action, string? reason)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound();
            if (action != "hide" && action != "restore") return BadRequest();
            var note = string.IsNullOrWhiteSpace(reason) ? "Nội dung chưa phù hợp quy định cộng đồng." : reason.Trim();
            if (note.Length > 500) return BadRequest("Lý do dài tối đa 500 ký tự.");
            comment.IsHidden = action == "hide";
            comment.HiddenAt = comment.IsHidden ? DateTime.UtcNow : null;
            comment.HiddenByAdminId = comment.IsHidden ? CurrentAdminId : null;
            comment.HiddenReason = comment.IsHidden ? note : null;
            LogAction("comment", id, action, note);
            if (action == "hide") _context.Notifications.Add(new Notification
            {
                UserId = comment.UserId, Title = "Bình luận được kiểm duyệt",
                Message = "Một bình luận của bạn đã bị ẩn. Lý do: " + note,
                Type = "system", ActionUrl = $"/Story/Detail/{comment.StoryId}#tabComments"
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = comment.IsHidden ? "Đã ẩn bình luận khỏi cộng đồng." : "Đã khôi phục bình luận.";
            return RedirectToAction("Index", new { tab = "commentsTab" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveReport(int id, string action, string? reason)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null) return NotFound();
            if (report.Status != "pending")
            {
                TempData["InfoMessage"] = "Báo cáo đã xử lý trước đó.";
                return RedirectToAction("Index", new { tab = "reportsTab" });
            }
            if (action != "hide_comment" && action != "reviewed" && action != "delete_target") return BadRequest();
            var note = string.IsNullOrWhiteSpace(reason) ? "Đã kiểm tra nội dung báo cáo." : reason.Trim();
            if (note.Length > 500) return BadRequest("Ghi chú tối đa 500 ký tự.");
            if (action == "hide_comment" || action == "delete_target")
            {
                // Soft hide; never physically delete a chapter (it may have user history and payments).
                if (report.TargetType != "comment") return BadRequest("Chỉ hỗ trợ ẩn bình luận; báo cáo truyện/chương cần xử lý thủ công.");
                var comment = await _context.Comments.FindAsync(report.TargetId);
                if (comment == null) return NotFound("Bình luận đã bị xóa khỏi CSDL.");
                comment.IsHidden = true;
                comment.HiddenAt = DateTime.UtcNow;
                comment.HiddenByAdminId = CurrentAdminId;
                comment.HiddenReason = note;
                LogAction("comment", comment.Id, "hide_by_report", note);
                _context.Notifications.Add(new Notification
                {
                    UserId = comment.UserId, Title = "Bình luận được kiểm duyệt",
                    Message = "Một bình luận đã bị ẩn sau khi xét duyệt báo cáo. Lý do: " + note,
                    Type = "system", ActionUrl = $"/Story/Detail/{comment.StoryId}#tabComments"
                });
            }
            report.Status = "resolved";
            report.ResolutionNotes = note;
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolvedByAdminId = CurrentAdminId;
            LogAction("report", id, "resolve", note);
            _context.Notifications.Add(new Notification { UserId = report.UserId, Type = "report", Title = "Báo cáo #" + id + " đã xử lý", Message = "Kết quả: " + note, ActionUrl = "/UserReport/Create" });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xử lý báo cáo.";
            return RedirectToAction("Index", new { tab = "reportsTab" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissReport(int id, string? reason)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null) return NotFound();
            if (report.Status != "pending") return RedirectToAction("Index", new { tab = "reportsTab" });
            var note = string.IsNullOrWhiteSpace(reason) ? "Không phát hiện vi phạm." : reason.Trim();
            if (note.Length > 500) return BadRequest();
            report.Status = "dismissed";
            report.ResolutionNotes = note;
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolvedByAdminId = CurrentAdminId;
            LogAction("report", id, "dismiss", note);
            _context.Notifications.Add(new Notification { UserId = report.UserId, Type = "report", Title = "Kết quả báo cáo #" + id, Message = "Báo cáo không được chấp nhận: " + note, ActionUrl = "/UserReport/Create" });
            await _context.SaveChangesAsync();
            TempData["InfoMessage"] = "Đã bác bỏ báo cáo.";
            return RedirectToAction("Index", new { tab = "reportsTab" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUserRole(int id, string role)
        {
            if (role != "reader" && role != "author") return BadRequest("Không hỗ trợ thay đổi quyền Admin ở đây.");
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (user.Role == "admin" || user.Id == CurrentAdminId) return Forbid();
            user.Role = role;
            LogAction("user", id, "change_role", role);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã đổi vai trò tài khoản {user.Email}.";
            return RedirectToAction("Index", new { tab = "usersTab" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetUserStatus(int id, string status, int days, string? reason)
        {
            if (status != "active" && status != "restricted" && status != "suspended" && status != "banned") return BadRequest();
            if (status == "restricted" || status == "suspended")
            {
                if (days < 1 || days > 365) return BadRequest("Thời gian phải từ 1 đến 365 ngày.");
            }
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (user.Role == "admin" || user.Id == CurrentAdminId) return Forbid();
            var note = string.IsNullOrWhiteSpace(reason) ? (status == "active" ? "Mở hạn chế theo quyết định Admin." : "Vi phạm quy định cộng đồng.") : reason.Trim();
            if (note.Length > 500) return BadRequest("Lý do tối đa 500 ký tự.");
            user.Status = status;
            user.StatusUntil = status == "restricted" || status == "suspended" ? DateTime.UtcNow.AddDays(days) : null;
            user.ModerationReason = status == "active" ? null : note;
            user.StatusChangedAt = DateTime.UtcNow;
            user.StatusChangedByAdminId = CurrentAdminId;
            LogAction("user", id, status, note + (user.StatusUntil.HasValue ? " - " + user.StatusUntil.Value.ToString("yyyy-MM-dd") : ""));
            _context.Notifications.Add(new Notification
            {
                UserId = id, Title = status == "active" ? "Đã khôi phục tài khoản" : "Thông báo trạng thái tài khoản",
                Message = status == "active" ? "Quyền sử dụng tài khoản đã được khôi phục." :
                    UserModeration.StatusMessage(user),
                Type = "system", ActionUrl = "/Account/Profile"
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái tài khoản {user.Email}: {status}.";
            return RedirectToAction("Index", new { tab = "usersTab" });
        }

        // Compatibility for older links and forms; no longer toggles blindly.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ToggleUserBan(int id) => SetUserStatus(id, "banned", 0, "Vi phạm quy định cộng đồng.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGenre(string name, string slug, string? icon)
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(slug))
            {
                _context.Genres.Add(new Genre
                {
                    Name = name.Trim(),
                    Slug = slug.Trim().ToLower(),
                    Icon = icon ?? "📖"
                });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tạo thể loại mới thành công!";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGenre(int id)
        {
            var genre = await _context.Genres.FindAsync(id);
            if (genre != null)
            {
                _context.Genres.Remove(genre);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa thể loại!";
            }
            return RedirectToAction("Index");
        }
    }
}
