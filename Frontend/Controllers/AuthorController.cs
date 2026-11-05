using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Hubs;
using ServerCSharp.Models;

namespace ServerCSharp.Controllers
{
    [Authorize(Roles = "author,admin")]
    public class AuthorController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<RealtimeHub> _hubContext;

        public AuthorController(AppDbContext context, IHubContext<RealtimeHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }

        private string Slugify(string text)
        {
            string str = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in str)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    if (c == 'đ') sb.Append('d');
                    else sb.Append(c);
                }
            }
            str = Regex.Replace(sb.ToString(), @"[^a-z0-9\s-]", "");
            str = Regex.Replace(str, @"\s+", "-").Trim('-');
            return str;
        }

        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            bool isAdmin = User.IsInRole("admin");
            var stories = await _context.Stories
                .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters)
                .Where(s => isAdmin || s.AuthorId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var genres = await _context.Genres.ToListAsync();

            ViewBag.AllGenres = genres;
            return View(stories);
        }

        [HttpPost]
        public async Task<IActionResult> CreateStory(string title, string description, string? coverUrl, string type, List<int>? genreIds)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập tên truyện và giới thiệu";
                return RedirectToAction("Index");
            }

            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);

            string baseSlug = Slugify(title);
            string slug = baseSlug;
            int counter = 1;
            while (await _context.Stories.AnyAsync(s => s.Slug == slug))
            {
                slug = $"{baseSlug}-{counter++}";
            }

            var story = new Story
            {
                Title = title.Trim(),
                Slug = slug,
                AuthorId = userId,
                AuthorName = user != null ? user.Name : "Tác giả",
                Description = description.Trim(),
                CoverUrl = string.IsNullOrWhiteSpace(coverUrl)
                    ? "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=500&auto=format&fit=crop&q=80"
                    : coverUrl.Trim(),
                Type = type == "comic" ? "comic" : "novel",
                Status = "ongoing",
                ApprovalStatus = "pending", // Pending approval by Admin
                Views = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Stories.Add(story);
            await _context.SaveChangesAsync();

            if (genreIds != null && genreIds.Count > 0)
            {
                foreach (var gId in genreIds)
                {
                    _context.StoryGenres.Add(new StoryGenre { StoryId = story.Id, GenreId = gId });
                }
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Đăng truyện thành công! Tác phẩm đang chờ Ban Quản Trị phê duyệt.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CreateChapter(int storyId, int? chapterNumber, string title, string? content, string? imageUrls, bool isFree = false, DateTime? publishAt = null)
        {
            var userId = GetCurrentUserId();
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            var story = await _context.Stories.FindAsync(storyId);
            if (story == null || (story.AuthorId != userId && role != "admin"))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập tên chương";
                return RedirectToAction("Index");
            }

            var urls = (imageUrls ?? "")
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (urls.Length > 100 || urls.Any(url =>
                !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)))
            {
                TempData["ErrorMessage"] = "Chỉ hỗ trợ tối đa 100 URL ảnh http/https hợp lệ, mỗi dòng một ảnh.";
                return RedirectToAction("Index");
            }

            string contentToSave;
            string? chapterImageUrlsJson = null;
            if (story.Type == "comic")
            {
                if (urls.Length == 0)
                {
                    TempData["ErrorMessage"] = "Truyện tranh cần ít nhất một URL ảnh.";
                    return RedirectToAction("Index");
                }
                // Tương thích các chương truyện tranh cũ: Content chứa JSON mảng URL.
                contentToSave = JsonSerializer.Serialize(urls);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    TempData["ErrorMessage"] = "Vui lòng nhập nội dung chương truyện chữ";
                    return RedirectToAction("Index");
                }
                contentToSave = content.Trim();
                // Truyện chữ có thể có ảnh minh họa sau phần văn bản.
                chapterImageUrlsJson = urls.Length == 0 ? null : JsonSerializer.Serialize(urls);
            }

            int number = chapterNumber ?? ((await _context.Chapters
                .Where(c => c.StoryId == storyId).MaxAsync(c => (int?)c.ChapterNumber)) ?? 0) + 1;
            if (number < 1 || await _context.Chapters.AnyAsync(c => c.StoryId == storyId && c.ChapterNumber == number))
            {
                TempData["ErrorMessage"] = "Số chương phải lớn hơn 0 và không được trùng.";
                return RedirectToAction("Index");
            }

            var chapter = new Chapter
            {
                StoryId = storyId,
                ChapterNumber = number,
                Title = title.Trim(),
                Content = contentToSave,
                ImageUrlsJson = chapterImageUrlsJson,
                IsFree = isFree,
                IsPublished = true, // Chương hẹn giờ tự hiện khi tới PublishAt.
                PublishAt = publishAt?.ToUniversalTime(),
                Views = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.Chapters.Add(chapter);
            story.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Chỉ thông báo khi chương đã phát hành; chương hẹn giờ sẽ chưa lộ nội dung.
            if (chapter.IsPublished && (!chapter.PublishAt.HasValue || chapter.PublishAt <= DateTime.UtcNow))
            {
                var followers = await _context.Favorites.Where(f => f.StoryId == storyId)
                    .Select(f => f.UserId).Distinct().ToListAsync();
                foreach (var followerId in followers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = followerId, Type = "chapter",
                        Title = "Truyện theo dõi có chương mới",
                        Message = $"{story.Title} vừa cập nhật Chương {chapter.ChapterNumber}: {chapter.Title}",
                        ActionUrl = $"/Reader/Read/{chapter.Id}"
                    });
                }
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    title = "Chương mới ra lò! ⚡",
                    message = $"Tác phẩm '{story.Title}' vừa cập nhật Chương {chapter.ChapterNumber}: {chapter.Title}!",
                    link = $"/Reader/Read/{chapter.Id}"
                });
            }

            TempData["SuccessMessage"] = $"Đã thêm Chương {chapter.ChapterNumber} thành công!";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> EditStory(int id)
        {
            var story = await _context.Stories.Include(s => s.Chapters).FirstOrDefaultAsync(s => s.Id == id);
            if (story == null) return NotFound();
            if (!User.IsInRole("admin") && story.AuthorId != GetCurrentUserId()) return Forbid();
            return View(story);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStory(int id, string title, string description, string? coverUrl, string status)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound();
            if (!User.IsInRole("admin") && story.AuthorId != GetCurrentUserId()) return Forbid();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) ||
                !new[] { "ongoing", "completed" }.Contains(status))
            {
                TempData["ErrorMessage"] = "Thông tin truyện chưa hợp lệ.";
            }
            else
            {
                story.Title = title.Trim(); story.Description = description.Trim();
                story.CoverUrl = coverUrl?.Trim(); story.Status = status; story.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(); TempData["SuccessMessage"] = "Đã cập nhật truyện.";
            }
            return RedirectToAction("EditStory", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateChapter(int id, string title, string? content, string? imageUrls, bool isFree = false, bool isPublished = false, DateTime? publishAt = null)
        {
            var chapter = await _context.Chapters.Include(c => c.Story).FirstOrDefaultAsync(c => c.Id == id);
            if (chapter?.Story == null) return NotFound();
            if (!User.IsInRole("admin") && chapter.Story.AuthorId != GetCurrentUserId()) return Forbid();
            var urls = (imageUrls ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (string.IsNullOrWhiteSpace(title) || urls.Length > 100 || urls.Any(url =>
                !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)) ||
                (chapter.Story.Type == "novel" && string.IsNullOrWhiteSpace(content)) ||
                (chapter.Story.Type == "comic" && urls.Length == 0))
            {
                TempData["ErrorMessage"] = "Nội dung hoặc URL ảnh chương không hợp lệ.";
            }
            else
            {
                chapter.Title = title.Trim(); chapter.IsFree = isFree; chapter.IsPublished = isPublished;
                chapter.PublishAt = publishAt?.ToUniversalTime();
                if (chapter.Story.Type == "comic") { chapter.Content = JsonSerializer.Serialize(urls); chapter.ImageUrlsJson = null; }
                else { chapter.Content = content!.Trim(); chapter.ImageUrlsJson = urls.Length == 0 ? null : JsonSerializer.Serialize(urls); }
                chapter.Story.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(); TempData["SuccessMessage"] = "Đã cập nhật chương.";
            }
            return RedirectToAction("EditStory", new { id = chapter.StoryId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStory(int id)
        {
            var userId = GetCurrentUserId();
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            var story = await _context.Stories.FindAsync(id);
            if (story != null && (story.AuthorId == userId || role == "admin"))
            {
                _context.Stories.Remove(story);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa bộ truyện thành công";
            }

            return RedirectToAction("Index");
        }
    }
}
