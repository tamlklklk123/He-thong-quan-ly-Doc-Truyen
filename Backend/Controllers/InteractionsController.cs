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

namespace ServerCSharp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api")]
    [Tags("Interactions - Tủ Sách & Lịch Sử Đọc")]
    public class InteractionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InteractionsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }

        // --- FAVORITES ---
        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorites()
        {
            var userId = GetCurrentUserId();
            var favorites = await _context.Favorites
                .Include(f => f.Story)
                    .ThenInclude(s => s!.Chapters)
                .Where(f => f.UserId == userId && f.Story != null)
                .Select(f => new StoryDto
                {
                    Id = f.Story!.Id,
                    Title = f.Story.Title,
                    Slug = f.Story.Slug,
                    AuthorId = f.Story.AuthorId,
                    AuthorName = f.Story.AuthorName,
                    Description = f.Story.Description,
                    CoverUrl = f.Story.CoverUrl,
                    Type = f.Story.Type,
                    Status = f.Story.Status,
                    ApprovalStatus = f.Story.ApprovalStatus,
                    Views = f.Story.Views,
                    CreatedAt = f.Story.CreatedAt,
                    ChapterCount = f.Story.Chapters.Count
                })
                .ToListAsync();

            return Ok(new { stories = favorites });
        }

        [HttpGet("favorites/check/{storyId}")]
        public async Task<IActionResult> CheckFavorite(int storyId)
        {
            var userId = GetCurrentUserId();
            var exists = await _context.Favorites.AnyAsync(f => f.UserId == userId && f.StoryId == storyId);
            return Ok(new { favorited = exists });
        }

        [HttpPost("favorites/toggle/{storyId}")]
        public async Task<IActionResult> ToggleFavorite(int storyId)
        {
            var userId = GetCurrentUserId();
            var existing = await _context.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.StoryId == storyId);

            if (existing != null)
            {
                _context.Favorites.Remove(existing);
                await _context.SaveChangesAsync();
                return Ok(new { favorited = false, message = "Đã xóa khỏi danh sách yêu thích" });
            }
            else
            {
                _context.Favorites.Add(new Favorite { UserId = userId, StoryId = storyId });
                await _context.SaveChangesAsync();
                return Ok(new { favorited = true, message = "Đã thêm vào tủ truyện yêu thích" });
            }
        }

        // --- READING HISTORY ---
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = GetCurrentUserId();
            var histories = await _context.ReadingHistories
                .Include(h => h.Story)
                .Include(h => h.Chapter)
                .Where(h => h.UserId == userId && h.Story != null && h.Chapter != null)
                .OrderByDescending(h => h.UpdatedAt)
                .Select(h => new
                {
                    id = h.Id,
                    story = new
                    {
                        id = h.Story!.Id,
                        title = h.Story.Title,
                        cover_url = h.Story.CoverUrl,
                        slug = h.Story.Slug,
                        type = h.Story.Type
                    },
                    chapter = new
                    {
                        id = h.Chapter!.Id,
                        chapter_number = h.Chapter.ChapterNumber,
                        title = h.Chapter.Title
                    },
                    updated_at = h.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { history = histories });
        }

        [HttpPost("history")]
        public async Task<IActionResult> SaveHistory([FromBody] SaveHistoryRequest request)
        {
            var userId = GetCurrentUserId();
            var existing = await _context.ReadingHistories.FirstOrDefaultAsync(h => h.UserId == userId && h.StoryId == request.StoryId);

            if (existing != null)
            {
                existing.ChapterId = request.ChapterId;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.ReadingHistories.Add(new ReadingHistory
                {
                    UserId = userId,
                    StoryId = request.StoryId,
                    ChapterId = request.ChapterId,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
