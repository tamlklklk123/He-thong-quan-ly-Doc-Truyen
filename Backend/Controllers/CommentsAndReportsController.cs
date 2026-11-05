using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.DTOs;
using ServerCSharp.Models;
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Community - Bình Luận")]
    public class CommentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CommentsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }

        [HttpGet]
        public async Task<IActionResult> GetComments([FromQuery] int? storyId, [FromQuery] int? chapterId)
        {
            var query = _context.Comments
                .Include(c => c.User)
                .Include(c => c.Chapter)
                .Where(c => !c.IsHidden)
                .AsQueryable();

            if (chapterId.HasValue)
            {
                query = query.Where(c => c.ChapterId == chapterId.Value);
            }
            else if (storyId.HasValue)
            {
                query = query.Where(c => c.StoryId == storyId.Value);
            }

            var comments = await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    id = c.Id,
                    content = c.Content,
                    created_at = c.CreatedAt,
                    user = new
                    {
                        id = c.User != null ? c.User.Id : 0,
                        name = c.User != null ? c.User.Name : "Ẩn danh",
                        avatar = c.User != null ? c.User.Avatar : "",
                        role = c.User != null ? c.User.Role : "reader"
                    },
                    chapter = c.Chapter != null ? new
                    {
                        id = c.Chapter.Id,
                        chapter_number = c.Chapter.ChapterNumber,
                        title = c.Chapter.Title
                    } : null
                })
                .ToListAsync();

            return Ok(new { comments });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] AddCommentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { message = "Nội dung bình luận không được để trống" });
            }

            var userId = GetCurrentUserId();
            var postingUser = await _context.Users.FindAsync(userId);
            if (!UserModeration.CanParticipate(postingUser)) return StatusCode(403, new { message = UserModeration.StatusMessage(postingUser) });
            if (request.Content.Trim().Length < 2 || request.Content.Trim().Length > 2000) return BadRequest(new { message = "Bình luận phải từ 2 đến 2000 ký tự." });
            if (await _context.Comments.AnyAsync(c => c.UserId == userId && c.CreatedAt > DateTime.UtcNow.AddSeconds(-15)))
                return StatusCode(429, new { message = "Bình luận quá nhanh, vui lòng thử lại sau 15 giây." });
            var story = await _context.Stories.FindAsync(request.StoryId);
            if (story == null) return NotFound(new { message = "Không tìm thấy truyện" });

            var comment = new Comment
            {
                UserId = userId,
                StoryId = request.StoryId,
                ChapterId = request.ChapterId,
                Content = request.Content.Trim()
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(userId);
            Chapter? chapter = null;
            if (comment.ChapterId.HasValue)
            {
                chapter = await _context.Chapters.FindAsync(comment.ChapterId.Value);
            }

            return StatusCode(201, new
            {
                message = "Đã gửi bình luận thành công",
                comment = new
                {
                    id = comment.Id,
                    content = comment.Content,
                    created_at = comment.CreatedAt,
                    user = new
                    {
                        id = user!.Id,
                        name = user.Name,
                        avatar = user.Avatar,
                        role = user.Role
                    },
                    chapter = chapter != null ? new
                    {
                        id = chapter.Id,
                        chapter_number = chapter.ChapterNumber,
                        title = chapter.Title
                    } : null
                }
            });
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var userId = GetCurrentUserId();
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound(new { message = "Không tìm thấy bình luận" });

            if (comment.UserId != userId && role != "admin")
            {
                return StatusCode(403, new { message = "Bạn không có quyền xóa bình luận này" });
            }

            comment.IsHidden = true;
            comment.HiddenAt = DateTime.UtcNow;
            comment.HiddenReason = "Ẩn theo yêu cầu thành viên hoặc Admin.";
            comment.HiddenByAdminId = role == "admin" ? userId : null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa bình luận thành công" });
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Community - Báo Cáo Vi Phạm")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }

        [HttpPost]
        public async Task<IActionResult> CreateReport([FromBody] ReportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TargetType) || string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new { message = "Vui lòng cung cấp đầy đủ thông tin báo cáo" });
            }

            var userId = GetCurrentUserId();
            var reportingUser = await _context.Users.FindAsync(userId);
            if (!UserModeration.CanParticipate(reportingUser)) return StatusCode(403, new { message = UserModeration.StatusMessage(reportingUser) });
            if (request.TargetType != "story" && request.TargetType != "chapter" && request.TargetType != "comment") return BadRequest(new { message = "Loại nội dung không hợp lệ" });
            if (request.Reason.Length > 250 || (request.Details?.Length ?? 0) > 2000) return BadRequest(new { message = "Nội dung báo cáo quá dài" });
            bool exists = request.TargetType == "comment" ? await _context.Comments.AnyAsync(c => c.Id == request.TargetId && !c.IsHidden) :
                request.TargetType == "story" ? await _context.Stories.AnyAsync(s => s.Id == request.TargetId) :
                await _context.Chapters.AnyAsync(c => c.Id == request.TargetId);
            if (!exists) return NotFound(new { message = "Không tìm thấy nội dung cần báo cáo" });
            if (await _context.Reports.AnyAsync(r => r.UserId == userId && r.TargetType == request.TargetType && r.TargetId == request.TargetId && r.Status == "pending"))
                return Conflict(new { message = "Báo cáo này của bạn đang chờ xử lý" });
            var report = new Report
            {
                UserId = userId,
                TargetType = request.TargetType,
                TargetId = request.TargetId,
                Reason = request.Reason.Trim(),
                Details = request.Details?.Trim() ?? "",
                Status = "pending"
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Báo cáo vi phạm của bạn đã được gửi tới Ban Quản Trị để xử lý. Cảm ơn bạn đã chung tay bảo vệ cộng đồng!",
                report
            });
        }
    }
}
