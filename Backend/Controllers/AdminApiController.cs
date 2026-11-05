using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;

namespace ServerCSharp.Controllers
{
    [ApiController]
    [Authorize(Roles = "admin")]
    [Route("api/admin")]
    [Tags("Admin - Quản Trị Hệ Thống")]
    public class AdminApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalStories = await _context.Stories.CountAsync();
            var totalChapters = await _context.Chapters.CountAsync();
            var pendingStoriesCount = await _context.Stories.CountAsync(s => s.ApprovalStatus == "pending");
            var pendingReportsCount = await _context.Reports.CountAsync(r => r.Status == "pending");
            var totalViews = await _context.Stories.SumAsync(s => s.Views);

            return Ok(new
            {
                totalUsers,
                totalStories,
                totalChapters,
                pendingStoriesCount,
                pendingReportsCount,
                totalViews
            });
        }

        [HttpGet("pending-stories")]
        public async Task<IActionResult> GetPendingStories()
        {
            var stories = await _context.Stories
                .Include(s => s.Author)
                .Include(s => s.Chapters)
                .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                .Where(s => s.ApprovalStatus == "pending")
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    s.Slug,
                    s.Type,
                    s.CoverUrl,
                    s.CreatedAt,
                    AuthorName = s.Author != null ? s.Author.Name : s.AuthorName,
                    ChapterCount = s.Chapters.Count,
                    Genres = s.StoryGenres.Select(sg => sg.Genre != null ? sg.Genre.Name : "")
                })
                .ToListAsync();

            return Ok(stories);
        }

        public class RejectRequest
        {
            public string? Reason { get; set; }
        }

        [HttpPost("stories/{id}/approve")]
        public async Task<IActionResult> ApproveStory(int id)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound(new { message = "Không tìm thấy truyện" });

            story.ApprovalStatus = "approved";
            story.RejectionReason = null;
            story.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã phê duyệt truyện thành công!", storyId = story.Id });
        }

        [HttpPost("stories/{id}/reject")]
        public async Task<IActionResult> RejectStory(int id, [FromBody] RejectRequest request)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound(new { message = "Không tìm thấy truyện" });

            story.ApprovalStatus = "rejected";
            story.RejectionReason = string.IsNullOrWhiteSpace(request?.Reason) ? "Nội dung chưa đạt tiêu chuẩn kiểm duyệt." : request.Reason;
            story.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã từ chối truyện!", storyId = story.Id, reason = story.RejectionReason });
        }

        [HttpGet("reports")]
        public async Task<IActionResult> GetReports()
        {
            var reports = await _context.Reports
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.TargetType,
                    r.TargetId,
                    r.Reason,
                    r.Details,
                    r.Status,
                    r.CreatedAt,
                    UserName = r.User != null ? r.User.Name : "Khách"
                })
                .ToListAsync();

            return Ok(reports);
        }

        [HttpPost("reports/{id}/resolve")]
        public async Task<IActionResult> ResolveReport(int id)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null) return NotFound(new { message = "Không tìm thấy báo cáo" });

            report.Status = "resolved";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xử lý báo cáo thành công!" });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Role,
                    Avatar = u.Avatar,
                    Status = u.Status,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }
    }
}
