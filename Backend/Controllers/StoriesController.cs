using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.DTOs;
using ServerCSharp.Models;

namespace ServerCSharp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Stories - Quản Lý Truyện")]
    public class StoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StoriesController(AppDbContext context)
        {
            _context = context;
        }

        private StoryDto MapStoryToDto(Story s)
        {
            return new StoryDto
            {
                Id = s.Id,
                Title = s.Title,
                Slug = s.Slug,
                AuthorId = s.AuthorId,
                AuthorName = s.AuthorName,
                Description = s.Description,
                CoverUrl = s.CoverUrl,
                Type = s.Type,
                Status = s.Status,
                ApprovalStatus = s.ApprovalStatus,
                RejectionReason = s.RejectionReason,
                Views = s.Views,
                AccessType = s.AccessType,
                Price = s.Price,
                AllowSubscription = s.AllowSubscription,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                ChapterCount = s.Chapters?.Count ?? 0,
                FavoritesCount = s.Favorites?.Count ?? 0,
                Genres = s.StoryGenres?.Select(sg => new GenreDto
                {
                    Id = sg.Genre?.Id ?? 0,
                    Name = sg.Genre?.Name ?? "",
                    Slug = sg.Genre?.Slug ?? ""
                }).ToList() ?? new List<GenreDto>()
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetStories(
            [FromQuery] string? genre,
            [FromQuery] string? search,
            [FromQuery] string? type,
            [FromQuery] string? status,
            [FromQuery] string sort = "latest",
            [FromQuery] int page = 1,
            [FromQuery] int limit = 12)
        {
            var query = _context.Stories
                .Include(s => s.StoryGenres)
                    .ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters)
                .Include(s => s.Favorites)
                .Where(s => s.ApprovalStatus == "approved");

            if (!string.IsNullOrWhiteSpace(genre))
            {
                query = query.Where(s => s.StoryGenres.Any(sg => sg.Genre != null && (sg.Genre.Slug == genre || sg.Genre.Id.ToString() == genre)));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(s => s.Type == type);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(s => s.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.ToLower().Trim();
                query = query.Where(s => s.Title.ToLower().Contains(q) || s.AuthorName.ToLower().Contains(q));
            }

            if (sort == "views")
            {
                query = query.OrderByDescending(s => s.Views);
            }
            else if (sort == "oldest")
            {
                query = query.OrderBy(s => s.CreatedAt);
            }
            else
            {
                query = query.OrderByDescending(s => s.UpdatedAt);
            }

            var total = await query.CountAsync();
            var stories = await query.Skip((page - 1) * limit).Take(limit).ToListAsync();

            return Ok(new
            {
                stories = stories.Select(MapStoryToDto),
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit)
            });
        }

        [HttpGet("featured")]
        public async Task<IActionResult> GetFeatured()
        {
            var approved = await _context.Stories
                .Include(s => s.StoryGenres)
                    .ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters)
                .Include(s => s.Favorites)
                .Where(s => s.ApprovalStatus == "approved")
                .ToListAsync();

            var topViews = approved.OrderByDescending(s => s.Views).Take(6).Select(MapStoryToDto).ToList();
            var latest = approved.OrderByDescending(s => s.CreatedAt).Take(6).Select(MapStoryToDto).ToList();

            return Ok(new { topViews, latest });
        }

        [HttpGet("{idOrSlug}")]
        public async Task<IActionResult> GetStoryDetail(string idOrSlug)
        {
            Story? story = null;
            if (int.TryParse(idOrSlug, out int id))
            {
                story = await _context.Stories
                    .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                    .Include(s => s.Chapters)
                    .Include(s => s.Favorites)
                    .FirstOrDefaultAsync(s => s.Id == id);
            }

            if (story == null)
            {
                story = await _context.Stories
                    .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                    .Include(s => s.Chapters)
                    .Include(s => s.Favorites)
                    .FirstOrDefaultAsync(s => s.Slug == idOrSlug);
            }

            if (story == null)
            {
                return NotFound(new { message = "Không tìm thấy truyện" });
            }

            // Increment views
            story.Views++;
            await _context.SaveChangesAsync();

            var chapters = await _context.Chapters
                .Where(c => c.StoryId == story.Id && c.IsPublished &&
                    (c.PublishAt == null || c.PublishAt <= DateTime.UtcNow))
                .OrderBy(c => c.ChapterNumber)
                .Select(c => new ChapterSimpleDto
                {
                    Id = c.Id,
                    ChapterNumber = c.ChapterNumber,
                    Title = c.Title,
                    IsFree = story.AccessType == "free" || c.IsFree,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            var dto = MapStoryToDto(story);

            return Ok(new
            {
                story = new StoryDetailDto
                {
                    Id = dto.Id,
                    Title = dto.Title,
                    Slug = dto.Slug,
                    AuthorId = dto.AuthorId,
                    AuthorName = dto.AuthorName,
                    Description = dto.Description,
                    CoverUrl = dto.CoverUrl,
                    Type = dto.Type,
                    Status = dto.Status,
                    ApprovalStatus = dto.ApprovalStatus,
                    RejectionReason = dto.RejectionReason,
                    Views = dto.Views,
                    AccessType = dto.AccessType,
                    Price = dto.Price,
                    AllowSubscription = dto.AllowSubscription,
                    CreatedAt = dto.CreatedAt,
                    UpdatedAt = dto.UpdatedAt,
                    ChapterCount = dto.ChapterCount,
                    FavoritesCount = dto.FavoritesCount,
                    Genres = dto.Genres,
                    Chapters = chapters
                }
            });
        }
    }
}
