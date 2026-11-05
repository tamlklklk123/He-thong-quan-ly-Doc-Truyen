using System.Collections.Generic;
using System.Linq;
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
    [Tags("Genres - Thể Loại Truyện")]
    public class GenresController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GenresController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/genres (Lấy danh sách tất cả các thể loại kèm số lượng truyện)
        [HttpGet]
        public async Task<IActionResult> GetGenres()
        {
            var genres = await _context.Genres
                .AsNoTracking()
                .Include(g => g.StoryGenres)
                .Select(g => new GenreDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Slug = g.Slug,
                    Description = g.Description,
                    Icon = g.Icon,
                    StoryCount = g.StoryGenres.Count
                })
                .OrderBy(g => g.Name)
                .ToListAsync();

            return Ok(genres);
        }

        // GET: api/genres/{id} (Xem chi tiết một thể loại)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGenre(int id)
        {
            var g = await _context.Genres
                .Include(x => x.StoryGenres)
                    .ThenInclude(sg => sg.Story)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (g == null)
            {
                return NotFound(new { message = "Không tìm thấy thể loại" });
            }

            var stories = g.StoryGenres
                .Where(sg => sg.Story != null && sg.Story.ApprovalStatus == "approved")
                .Select(sg => new StorySimpleDto
                {
                    Id = sg.Story!.Id,
                    Title = sg.Story.Title,
                    Slug = sg.Story.Slug,
                    Type = sg.Story.Type,
                    AuthorName = sg.Story.AuthorName
                })
                .ToList();

            return Ok(new
            {
                genre = new GenreDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Slug = g.Slug,
                    Description = g.Description,
                    Icon = g.Icon,
                    StoryCount = stories.Count
                },
                stories
            });
        }

        // POST: api/genres (Admin thêm thể loại mới)
        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> CreateGenre([FromBody] CreateGenreRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Tên thể loại không được để trống" });
            }

            var slug = string.IsNullOrWhiteSpace(request.Slug)
                ? request.Name.ToLower().Replace(" ", "-")
                : request.Slug.ToLower().Trim();

            var exists = await _context.Genres.AnyAsync(g => g.Slug == slug || g.Name.ToLower() == request.Name.ToLower());
            if (exists)
            {
                return BadRequest(new { message = "Thể loại này đã tồn tại" });
            }

            var genre = new Genre
            {
                Name = request.Name.Trim(),
                Slug = slug,
                Description = request.Description?.Trim(),
                Icon = string.IsNullOrWhiteSpace(request.Icon) ? "📖" : request.Icon.Trim()
            };

            _context.Genres.Add(genre);
            await _context.SaveChangesAsync();

            return StatusCode(201, new GenreDto
            {
                Id = genre.Id,
                Name = genre.Name,
                Slug = genre.Slug,
                Description = genre.Description,
                Icon = genre.Icon,
                StoryCount = 0
            });
        }

        // DELETE: api/genres/{id} (Admin xóa thể loại)
        [Authorize(Roles = "admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGenre(int id)
        {
            var genre = await _context.Genres.FindAsync(id);
            if (genre == null) return NotFound(new { message = "Không tìm thấy thể loại" });

            _context.Genres.Remove(genre);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa thể loại thành công" });
        }
    }
}
