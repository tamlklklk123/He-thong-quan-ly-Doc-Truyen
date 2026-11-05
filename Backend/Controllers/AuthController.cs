using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ServerCSharp.Data;
using ServerCSharp.DTOs;
using ServerCSharp.Models;
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Auth - Xác Thực & Phân Quyền")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "web_doc_truyen_secret_key_csharp_2026_super_secure!";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Vui lòng điền đầy đủ họ tên, email và mật khẩu" });
            }

            var existing = await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower().Trim());
            if (existing)
            {
                return BadRequest(new { message = "Email này đã được sử dụng" });
            }

            var allowedRole = (request.Role == "author") ? "author" : "reader";

            var user = new User
            {
                Name = request.Name.Trim(),
                Email = request.Email.ToLower().Trim(),
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = allowedRole,
                Status = "active",
                Avatar = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(request.Name)}",
                Bio = ""
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return StatusCode(201, new AuthResponse
            {
                Message = "Đăng ký tài khoản thành công",
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Role = user.Role,
                    Avatar = user.Avatar,
                    Status = user.Status,
                    CreatedAt = user.CreatedAt
                }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Vui lòng nhập email và mật khẩu" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower().Trim());
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                return Unauthorized(new { message = "Email hoặc mật khẩu không chính xác" });
            }

            if (UserModeration.IsLoginBlocked(user))
            {
                return StatusCode(403, new { message = UserModeration.StatusMessage(user) });
            }

            var token = GenerateJwtToken(user);

            return Ok(new AuthResponse
            {
                Message = "Đăng nhập thành công",
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Role = user.Role,
                    Avatar = user.Avatar,
                    Status = user.Status,
                    CreatedAt = user.CreatedAt
                }
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Vui lòng nhập email và mật khẩu mới" });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower().Trim());
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy tài khoản với email này" });
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đặt lại mật khẩu thành công!", email = user.Email });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ" });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng" });
            }

            return Ok(new
            {
                user = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Role = user.Role,
                    Avatar = user.Avatar,
                    Bio = user.Bio,
                    Status = user.Status,
                    CreatedAt = user.CreatedAt
                }
            });
        }

        [Authorize]
        [HttpPost("become-author")]
        public async Task<IActionResult> BecomeAuthor()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.Role == "admin")
            {
                return BadRequest(new { message = "Tài khoản Admin đã có quyền cao nhất" });
            }

            user.Role = "author";
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return Ok(new AuthResponse
            {
                Message = "Chúc mừng! Bạn đã được nâng cấp thành Tác giả, bây giờ có thể đăng truyện mới.",
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Role = user.Role,
                    Avatar = user.Avatar,
                    Status = user.Status,
                    CreatedAt = user.CreatedAt
                }
            });
        }
    }
}
