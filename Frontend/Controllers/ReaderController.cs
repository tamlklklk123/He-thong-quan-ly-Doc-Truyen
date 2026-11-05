using System;
using System.Linq;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Models;
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    public class ReaderController : Controller
    {
        private readonly AppDbContext _context;

        public ReaderController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Read(int id)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (chapter == null || chapter.Story == null) return NotFound();

            int? accessUserId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var viewerId) ? viewerId : null;
            if (!await ReadingAccessService.CanReadAsync(_context, chapter.Story, chapter, accessUserId, User.IsInRole("admin")))
            {
                if (!ReadingAccessService.IsVisible(chapter)) return NotFound("Chương chưa công bố.");
                TempData["ErrorMessage"] = "Chương trả phí: dùng 20 Xu, vé mở chương, mua truyện hoặc đăng ký VIP để đọc.";
                return RedirectToAction("Index", "Billing", new { storyId = chapter.StoryId, chapterId = chapter.Id });
            }

            // Ghi đọc và thưởng điểm trong một giao dịch; đọc lại không được thưởng thêm.
            await using var readingTx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            // Increment views
            chapter.Views++;

            // Auto save reading history if logged in
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    var awarded = await _context.ChapterReadCredits.AnyAsync(x => x.UserId == userId && x.ChapterId == chapter.Id);
                    if (!awarded)
                    {
                        var member = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                        if (member != null && !UserModeration.IsLoginBlocked(member))
                        {
                            _context.ChapterReadCredits.Add(new ChapterReadCredit { UserId = userId, ChapterId = chapter.Id,
                                PointsAwarded = ReaderPointsService.PointsPerUniqueChapter });
                            checked { member.LoyaltyPoints += ReaderPointsService.PointsPerUniqueChapter; }
                            ViewBag.NewPoints = ReaderPointsService.PointsPerUniqueChapter;
                        }
                    }
                    var week = ReaderMembershipService.VietnamWeekStart(DateTime.UtcNow);
                    if (!await _context.WeeklyChapterReads.AnyAsync(r => r.UserId == userId && r.ChapterId == chapter.Id && r.WeekStartDate == week))
                        _context.WeeklyChapterReads.Add(new WeeklyChapterRead { UserId = userId, ChapterId = chapter.Id, WeekStartDate = week });
                    var existingHistory = await _context.ReadingHistories.FirstOrDefaultAsync(h => h.UserId == userId && h.StoryId == chapter.StoryId);
                    if (existingHistory != null)
                    {
                        existingHistory.ChapterId = chapter.Id;
                        existingHistory.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.ReadingHistories.Add(new ReadingHistory
                        {
                            UserId = userId,
                            StoryId = chapter.StoryId,
                            ChapterId = chapter.Id,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            await readingTx.CommitAsync();

            var allChapters = await _context.Chapters
                .Where(c => c.StoryId == chapter.StoryId && c.IsPublished &&
                    (c.PublishAt == null || c.PublishAt <= DateTime.UtcNow))
                .OrderBy(c => c.ChapterNumber)
                .ToListAsync();

            var currentIndex = allChapters.FindIndex(c => c.Id == chapter.Id);
            Chapter? prev = currentIndex > 0 ? allChapters[currentIndex - 1] : null;
            Chapter? next = currentIndex < allChapters.Count - 1 ? allChapters[currentIndex + 1] : null;

            var currentUser = accessUserId.HasValue ? await _context.Users.FindAsync(accessUserId.Value) : null;
            ViewBag.CanDiscuss = UserModeration.CanParticipate(currentUser);
            ViewBag.DiscussionRestriction = currentUser == null ? "" : UserModeration.StatusMessage(currentUser);
            ViewBag.PrevChapter = prev;
            ViewBag.NextChapter = next;
            ViewBag.AllChapters = allChapters;
            ViewBag.ChapterComments = await _context.Comments.Include(c => c.User)
                .Where(c => c.ChapterId == chapter.Id && !c.IsHidden)
                .OrderByDescending(c => c.CreatedAt).Take(100).ToListAsync();

            return View(chapter);
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddChapterComment(int id, string content)
        {
            if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 2000)
            { TempData["ErrorMessage"] = "Bình luận phải từ 1 đến 2000 ký tự."; return RedirectToAction("Read", new { id }); }
            var chapter = await _context.Chapters.Include(c => c.Story).FirstOrDefaultAsync(c => c.Id == id);
            if (chapter?.Story == null) return NotFound();
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)) return Unauthorized();
            if (!await ReadingAccessService.CanReadAsync(_context, chapter.Story, chapter, userId, User.IsInRole("admin")))
                return Forbid();
            var user = await _context.Users.FindAsync(userId);
            if (!UserModeration.CanParticipate(user))
            {
                TempData["ErrorMessage"] = UserModeration.StatusMessage(user);
                return RedirectToAction("Read", new { id });
            }
            if (await _context.Comments.AnyAsync(c => c.UserId == userId && c.CreatedAt > DateTime.UtcNow.AddSeconds(-15)))
            {
                TempData["ErrorMessage"] = "Bạn gửi bình luận quá nhanh, hãy đợi 15 giây.";
                return RedirectToAction("Read", new { id });
            }
            _context.Comments.Add(new Comment { ChapterId = chapter.Id, StoryId = chapter.StoryId,
                UserId = userId, Content = content.Trim(), CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();
            return Redirect($"/Reader/Read/{id}#chapter-comments");
        }
    }
}
