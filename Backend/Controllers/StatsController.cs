using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.DTOs;

namespace ServerCSharp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Stats & Leaderboard - Thống Kê & Bảng Xếp Hạng")]
    public class StatsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StatsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/stats/dashboard (Tổng quan toàn bộ số liệu hệ thống)
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalStories = await _context.Stories.CountAsync(s => s.ApprovalStatus == "approved");
            var totalChapters = await _context.Chapters.CountAsync();
            var totalViews = await _context.Stories.SumAsync(s => (long)s.Views);
            var totalComments = await _context.Comments.CountAsync();
            var totalPendingApproval = await _context.Stories.CountAsync(s => s.ApprovalStatus == "pending");
            var totalPendingReports = await _context.Reports.CountAsync(r => r.Status == "pending");

            return Ok(new DashboardStatsDto
            {
                TotalUsers = totalUsers,
                TotalStories = totalStories,
                TotalChapters = totalChapters,
                TotalViews = (int)totalViews,
                TotalComments = totalComments,
                TotalPendingApproval = totalPendingApproval,
                TotalPendingReports = totalPendingReports
            });
        }

        // GET: api/stats/top-views (Bảng xếp hạng theo lượt xem nhiều nhất)
        [HttpGet("top-views")]
        public async Task<IActionResult> GetTopViews([FromQuery] int limit = 10, [FromQuery] string? type = null)
        {
            var query = _context.Stories
                .AsNoTracking()
                .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                .Where(s => s.ApprovalStatus == "approved");

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(s => s.Type == type);
            }

            var stories = await query
                .OrderByDescending(s => s.Views)
                .Take(Math.Min(limit, 50))
                .ToListAsync();

            var result = stories.Select((s, index) => new LeaderboardItemDto
            {
                Rank = index + 1,
                StoryId = s.Id,
                Title = s.Title,
                Slug = s.Slug,
                AuthorName = s.AuthorName,
                CoverUrl = s.CoverUrl,
                Score = s.Views,
                ScoreLabel = "Lượt xem",
                Genres = s.StoryGenres.Select(sg => sg.Genre?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList()
            }).ToList();

            return Ok(result);
        }

        // GET: api/stats/top-favorites (Bảng xếp hạng truyện được yêu thích / lưu vào tủ sách nhiều nhất)
        [HttpGet("top-favorites")]
        public async Task<IActionResult> GetTopFavorites([FromQuery] int limit = 10)
        {
            var stories = await _context.Stories
                .AsNoTracking()
                .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Favorites)
                .Where(s => s.ApprovalStatus == "approved")
                .OrderByDescending(s => s.Favorites.Count)
                .Take(Math.Min(limit, 50))
                .ToListAsync();

            var result = stories.Select((s, index) => new LeaderboardItemDto
            {
                Rank = index + 1,
                StoryId = s.Id,
                Title = s.Title,
                Slug = s.Slug,
                AuthorName = s.AuthorName,
                CoverUrl = s.CoverUrl,
                Score = s.Favorites.Count,
                ScoreLabel = "Lượt lưu tủ truyện",
                Genres = s.StoryGenres.Select(sg => sg.Genre?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList()
            }).ToList();

            return Ok(result);
        }

        // GET: api/stats/top-authors (Bảng xếp hạng tác giả tiêu biểu)
        [HttpGet("top-authors")]
        public async Task<IActionResult> GetTopAuthors([FromQuery] int limit = 10)
        {
            var authors = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == "author" && u.Status == "active")
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Avatar,
                    u.Bio,
                    StoryCount = _context.Stories.Count(s => s.AuthorId == u.Id && s.ApprovalStatus == "approved"),
                    TotalViews = _context.Stories.Where(s => s.AuthorId == u.Id && s.ApprovalStatus == "approved").Sum(s => (long)s.Views)
                })
                .OrderByDescending(a => a.TotalViews)
                .Take(Math.Min(limit, 20))
                .ToListAsync();

            return Ok(authors);
        }
    }
}
