using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Models;
using ServerCSharp.Services;
using Microsoft.AspNetCore.Authorization;

namespace ServerCSharp.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Login(string? returnUrl, string? mode)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Mode = mode;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> LoginPost(string email, string password, bool rememberMe, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ email và mật khẩu";
                return RedirectToAction("Login", new { returnUrl });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower().Trim());
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                TempData["ErrorMessage"] = "Email hoặc mật khẩu không chính xác";
                return RedirectToAction("Login", new { returnUrl });
            }

            if (UserModeration.IsLoginBlocked(user))
            {
                TempData["ErrorMessage"] = UserModeration.StatusMessage(user);
                return RedirectToAction("Login", new { returnUrl });
            }

            await SignInUser(user, rememberMe);
            TempData["SuccessMessage"] = $"Chào mừng trở lại, {user.Name}!";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return RedirectToAction("Login", new { mode = "forgot" });
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPasswordPost(string email, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                TempData["ErrorMessage"] = "Vui lòng điền đầy đủ email và mật khẩu mới";
                return RedirectToAction("Login", new { mode = "forgot" });
            }

            if (newPassword.Length < 6)
            {
                TempData["ErrorMessage"] = "Mật khẩu mới phải có tối thiểu 6 ký tự";
                return RedirectToAction("Login", new { mode = "forgot" });
            }

            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "Xác nhận mật khẩu không trùng khớp";
                return RedirectToAction("Login", new { mode = "forgot" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower().Trim());
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản với email này trong hệ thống";
                return RedirectToAction("Login", new { mode = "forgot" });
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Khôi phục mật khẩu thành công cho tài khoản {user.Email}! Hãy đăng nhập với mật khẩu mới.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> RegisterPost(string name, string email, string password, string role)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "Vui lòng điền đầy đủ họ tên, email và mật khẩu";
                return RedirectToAction("Login");
            }

            var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower().Trim());
            if (exists)
            {
                TempData["ErrorMessage"] = "Email này đã được sử dụng";
                return RedirectToAction("Login");
            }

            var allowedRole = (role == "author") ? "author" : "reader";
            var user = new User
            {
                Name = name.Trim(),
                Email = email.ToLower().Trim(),
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                Role = allowedRole,
                Status = "active",
                Avatar = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(name)}",
                Bio = ""
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await SignInUser(user);
            TempData["SuccessMessage"] = $"Đăng ký thành công tài khoản {user.Name}!";

            return RedirectToAction("Index", "Home");
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickLogin(string? email, string? role)
        {
            User? user = null;
            if (!string.IsNullOrWhiteSpace(email))
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower().Trim());
            }
            else if (!string.IsNullOrWhiteSpace(role))
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Role.ToLower() == role.ToLower().Trim());
            }

            if (user != null && !UserModeration.IsLoginBlocked(user))
            {
                await SignInUser(user);
                TempData["SuccessMessage"] = $"Đã chuyển nhanh sang tài khoản {user.Name} ({user.Role.ToUpper()})!";

                var referer = Request.Headers["Referer"].ToString();
                if (!string.IsNullOrWhiteSpace(referer) && !referer.Contains("/Account/Login"))
                {
                    return Redirect(referer);
                }

                if (user.Role == "admin") return RedirectToAction("Index", "Admin");
                if (user.Role == "author") return RedirectToAction("Index", "Author");
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["InfoMessage"] = "Đã đăng xuất thành công";
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            int userId = 0;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(userIdClaim, out userId);
            }

            User? user = null;
            if (userId > 0)
            {
                user = await _context.Users.FindAsync(userId);
            }
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            ViewBag.IsVip = await ReaderMembershipService.IsVipAsync(_context, user.Id);
            ViewBag.UnlockedChapters = await _context.CoinChapterUnlocks.CountAsync(x => x.UserId == user.Id);
            ViewBag.Badges = await _context.BookwormBadges.AsNoTracking().Where(b => b.UserId == user.Id)
                .OrderByDescending(b => b.WeekStartDate).ToListAsync();
            return View(user);
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string name, string bio, string? newPassword)
        {
            int userId = 0;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(userIdClaim, out userId);
            }

            var user = userId > 0 ? await _context.Users.FindAsync(userId) : null;
            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(name)) user.Name = name.Trim();
                user.Bio = bio?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(newPassword) && newPassword.Length >= 6)
                {
                    user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật thông tin tài khoản thành công!";
            }
            return RedirectToAction("Profile");
        }

        private async Task SignInUser(User user, bool isPersistent = false)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = isPersistent ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(4)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
        }
    }
}
