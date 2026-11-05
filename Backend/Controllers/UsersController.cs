using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BCrypt.Net;
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
    [Tags("Users & Profile - Người Dùng & Hồ Sơ")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : null;
        }

        // GET: api/users/profile (Xem hồ sơ cá nhân đầy đủ kèm thống kê cá nhân)
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            var totalStories = await _context.Stories.CountAsync(s => s.AuthorId == userId.Value);
            var totalFavorites = await _context.Favorites.CountAsync(f => f.UserId == userId.Value);
            var totalHistory = await _context.ReadingHistories.CountAsync(h => h.UserId == userId.Value);
            var totalComments = await _context.Comments.CountAsync(c => c.UserId == userId.Value);

            var profile = new UserProfileDetailDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                Avatar = user.Avatar,
                Bio = user.Bio,
                CreatedAt = user.CreatedAt,
                TotalStoriesPublished = totalStories,
                TotalFavoritesCount = totalFavorites,
                TotalReadingHistoryCount = totalHistory,
                TotalCommentsCount = totalComments
            };

            return Ok(profile);
        }

        // PUT: api/users/profile (Cập nhật thông tin họ tên, tiểu sử, ảnh đại diện)
        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                user.Name = request.Name.Trim();
            }

            if (request.Bio != null)
            {
                user.Bio = request.Bio.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Avatar))
            {
                user.Avatar = request.Avatar.Trim();
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật hồ sơ thành công",
                user = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Role = user.Role,
                    Status = user.Status,
                    Avatar = user.Avatar,
                    Bio = user.Bio,
                    CreatedAt = user.CreatedAt
                }
            });
        }

        // PUT: api/users/change-password (Đổi mật khẩu tài khoản)
        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Vui lòng nhập mật khẩu hiện tại và mật khẩu mới" });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự" });
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password))
            {
                return BadRequest(new { message = "Mật khẩu hiện tại không chính xác" });
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công!" });
        }

        // GET: api/users/{id} (Xem thông tin công khai của một tác giả / độc giả)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPublicProfile(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || user.Status == "banned")
            {
                return NotFound(new { message = "Không tìm thấy thông tin thành viên này" });
            }

            var publicStories = await _context.Stories
                .Where(s => s.AuthorId == id && s.ApprovalStatus == "approved")
                .OrderByDescending(s => s.Views)
                .Select(s => new StorySimpleDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Slug = s.Slug,
                    Type = s.Type,
                    AuthorName = s.AuthorName
                })
                .ToListAsync();

            return Ok(new
            {
                user = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Role = user.Role,
                    Status = user.Status,
                    Avatar = user.Avatar,
                    Bio = user.Bio,
                    CreatedAt = user.CreatedAt
                },
                stories = publicStories
            });
        }
    }
}
