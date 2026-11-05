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
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    public class StoryController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<RealtimeHub> _hubContext;

        public StoryController(AppDbContext context, IHubContext<RealtimeHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Detail(int id)
        {
            var story = await _context.Stories
                .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters)
                .Include(s => s.Comments.Where(c => !c.IsHidden)).ThenInclude(c => c.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (story == null) return NotFound();
            if (story.ApprovalStatus != "approved" && !User.IsInRole("admin") &&
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value != story.AuthorId.ToString()) return NotFound();
            story.Chapters = story.Chapters.Where(ReadingAccessService.IsVisible)
                .OrderBy(c => c.ChapterNumber).ToList();

            // Increment views
            story.Views++;
            await _context.SaveChangesAsync();

            bool isFavorited = false;
            Chapter? resumeChapter = null;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    isFavorited = await _context.Favorites.AnyAsync(f => f.UserId == userId && f.StoryId == id);
                    var history = await _context.ReadingHistories.Include(h => h.Chapter).FirstOrDefaultAsync(h => h.UserId == userId && h.StoryId == id);
                    if (history != null) resumeChapter = history.Chapter;
                }
            }

            ViewBag.IsFavorited = isFavorited;
            ViewBag.ResumeChapter = resumeChapter;
            ViewBag.FirstChapter = story.Chapters.OrderBy(c => c.ChapterNumber).FirstOrDefault();
            var myClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var myAccount = int.TryParse(myClaim, out int myId)
                ? await _context.Users.FindAsync(myId) : null;
            ViewBag.CanDiscuss = UserModeration.CanParticipate(myAccount);
            ViewBag.DiscussionRestriction = myAccount == null ? "" : UserModeration.StatusMessage(myAccount);

            var ratings = await _context.StoryRatings
                .Include(r => r.User)
                .Where(r => r.StoryId == id)
                .OrderByDescending(r => r.UpdatedAt)
                .ToListAsync();
            ViewBag.StoryRatings = ratings;
            ViewBag.RatingCount = ratings.Count;
            ViewBag.RatingAverage = ratings.Count == 0 ? 0.0 : ratings.Average(r => r.Score);

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            ViewBag.MyRating = int.TryParse(currentUserId, out int ratingUserId)
                ? ratings.FirstOrDefault(r => r.UserId == ratingUserId)
                : null;

            return View(story);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateStory(int storyId, int score, string? review)
        {
            if (score < 1 || score > 5)
            {
                TempData["ErrorMessage"] = "Số sao phải từ 1 đến 5.";
                return Redirect($"/Story/Detail/{storyId}#tabRatings");
            }

            if (!await _context.Stories.AnyAsync(s => s.Id == storyId))
                return NotFound();

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId)) return Unauthorized();
            var actor = await _context.Users.FindAsync(userId);
            if (!UserModeration.CanParticipate(actor))
            {
                TempData["ErrorMessage"] = UserModeration.StatusMessage(actor);
                return Redirect($"/Story/Detail/{storyId}#tabRatings");
            }
            var trimmedReview = review?.Trim();
            if (trimmedReview?.Length > 1500)
            {
                TempData["ErrorMessage"] = "Nhận xét tối đa 1500 ký tự.";
                return Redirect($"/Story/Detail/{storyId}#tabRatings");
            }

            var current = await _context.StoryRatings
                .FirstOrDefaultAsync(r => r.StoryId == storyId && r.UserId == userId);
            if (current == null)
            {
                _context.StoryRatings.Add(new StoryRating
                {
                    StoryId = storyId,
                    UserId = userId,
                    Score = score,
                    Review = trimmedReview,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                current.Score = score;
                current.Review = trimmedReview;
                current.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã lưu đánh giá của bạn!";
            return Redirect($"/Story/Detail/{storyId}#tabRatings");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = $"/Story/Detail/{id}" });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var existing = await _context.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.StoryId == id);
            if (existing != null)
            {
                _context.Favorites.Remove(existing);
                TempData["InfoMessage"] = "Đã xóa khỏi tủ truyện yêu thích";
            }
            else
            {
                _context.Favorites.Add(new Favorite { UserId = userId, StoryId = id });
                TempData["SuccessMessage"] = "Đã lưu vào tủ truyện yêu thích!";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int storyId, string content)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(claim, out int userId)) return Unauthorized();
            var user = await _context.Users.FindAsync(userId);
            if (!UserModeration.CanParticipate(user))
            {
                TempData["ErrorMessage"] = UserModeration.StatusMessage(user);
                return RedirectToAction("Detail", new { id = storyId });
            }
            var trimmed = content?.Trim() ?? "";
            if (trimmed.Length < 2 || trimmed.Length > 2000)
            {
                TempData["ErrorMessage"] = "Bình luận phải có từ 2 đến 2000 ký tự.";
                return RedirectToAction("Detail", new { id = storyId });
            }
            if (!await _context.Stories.AnyAsync(s => s.Id == storyId && s.ApprovalStatus == "approved")) return NotFound();
            var cutoff = DateTime.UtcNow.AddSeconds(-15);
            var recent = await _context.Comments.AnyAsync(c => c.UserId == userId && c.CreatedAt >= cutoff);
            if (recent)
            {
                TempData["ErrorMessage"] = "Bạn gửi bình luận quá nhanh, vui lòng thử lại sau 15 giây.";
                return RedirectToAction("Detail", new { id = storyId });
            }
            var comment = new Comment { UserId = userId, StoryId = storyId, Content = trimmed };
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"story_{storyId}").SendAsync("ReceiveComment", new
            {
                id = comment.Id, storyId, userName = user!.Name, userAvatar = user.Avatar,
                userRole = user.Role, content = trimmed, createdAt = "Vừa xong"
            });
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
            {
                title = "Bình luận mới 💬", message = $"{user.Name} vừa bình luận về truyện.",
                link = $"/Story/Detail/{storyId}#tabComments"
            });
            TempData["SuccessMessage"] = "Đã đăng bình luận!";
            return RedirectToAction("Detail", new { id = storyId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id, int storyId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (int.TryParse(userIdClaim, out int userId))
            {
                var comment = await _context.Comments.FindAsync(id);
                if (comment != null && (comment.UserId == userId || role == "admin"))
                {
                    comment.IsHidden = true;
                    comment.HiddenReason = "Đã ẩn theo yêu cầu của tác giả bình luận hoặc Admin.";
                    comment.HiddenAt = DateTime.UtcNow;
                    comment.HiddenByAdminId = role == "admin" ? userId : null;
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Đã xóa bình luận!";
                }
            }

            return RedirectToAction("Detail", new { id = storyId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report(int targetId, string targetType, string reason, string? details, int storyId)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = $"/Story/Detail/{storyId}" });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (!UserModeration.CanParticipate(user))
                {
                    TempData["ErrorMessage"] = UserModeration.StatusMessage(user);
                    return RedirectToAction("Detail", new { id = storyId });
                }
                if (targetType != "comment" && targetType != "story" && targetType != "chapter") return BadRequest();
                if (string.IsNullOrWhiteSpace(reason) || reason.Length > 250 || (details?.Length ?? 0) > 2000) return BadRequest();
                bool exists = targetType == "comment" ? await _context.Comments.AnyAsync(x => x.Id == targetId && !x.IsHidden) :
                    targetType == "story" ? await _context.Stories.AnyAsync(x => x.Id == targetId) :
                    await _context.Chapters.AnyAsync(x => x.Id == targetId);
                if (!exists) return NotFound();
                var duplicate = await _context.Reports.AnyAsync(x => x.UserId == userId && x.TargetId == targetId && x.TargetType == targetType && x.Status == "pending");
                if (duplicate)
                {
                    TempData["InfoMessage"] = "Bạn đã gửi báo cáo cho nội dung này, đang chờ Admin xử lý.";
                    return RedirectToAction("Detail", new { id = storyId });
                }
                _context.Reports.Add(new Report
                {
                    UserId = userId,
                    TargetType = targetType,
                    TargetId = targetId,
                    Reason = reason,
                    Details = details ?? "",
                    Status = "pending"
                });
                await _context.SaveChangesAsync();
                var adminIds = await _context.Users.Where(u => u.Role == "admin").Select(u => u.Id).ToListAsync();
                foreach (var adminId in adminIds) _context.Notifications.Add(new Notification
                {
                    UserId = adminId, Type = "report", Title = "Báo cáo vi phạm mới",
                    Message = $"Có báo cáo {targetType}: {reason}", ActionUrl = "/Admin?tab=reportsTab"
                });
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Báo cáo vi phạm của bạn đã được gửi tới Ban Quản Trị để xử lý. Cảm ơn bạn!";
            }

            return RedirectToAction("Detail", new { id = storyId });
        }
    }
}
