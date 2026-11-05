using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Models;
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var approvedStories = await _context.Stories
                .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters)
                .Where(s => s.ApprovalStatus == "approved")
                .ToListAsync();

            var topViews = approvedStories.OrderByDescending(s => s.Views).Take(6).ToList();
            var latest = approvedStories.OrderByDescending(s => s.CreatedAt).Take(8).ToList();
            var genres = await _context.Genres
                .Select(g => new
                {
                    Genre = g,
                    Count = g.StoryGenres.Count(sg => sg.Story != null && sg.Story.ApprovalStatus == "approved")
                })
                .ToListAsync();

            ViewBag.TopViews = topViews;
            ViewBag.Latest = latest;
            ViewBag.Genres = genres;
            ViewBag.HeroStory = topViews.FirstOrDefault();

            return View();
        }

        public async Task<IActionResult> Genres(string? genre, string? search, string? type, string? status)
        {
            var query = _context.Stories
                .Include(s => s.StoryGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters)
                .Where(s => s.ApprovalStatus == "approved");

            if (!string.IsNullOrWhiteSpace(genre))
            {
                query = query.Where(s => s.StoryGenres.Any(sg => sg.Genre != null && sg.Genre.Slug == genre));
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

            var stories = await query.OrderByDescending(s => s.UpdatedAt).ToListAsync();
            var allGenres = await _context.Genres.Include(g => g.StoryGenres).ToListAsync();

            ViewBag.CurrentGenre = genre;
            ViewBag.CurrentType = type;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;
            ViewBag.AllGenres = allGenres;

            return View(stories);
        }

        public async Task<IActionResult> Favorites()
        {
            int userId = 0;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(userIdClaim, out userId);
            }

            var favorites = userId > 0
                ? await _context.Favorites
                    .Include(f => f.Story).ThenInclude(s => s!.Chapters)
                    .Where(f => f.UserId == userId && f.Story != null)
                    .Select(f => f.Story!)
                    .ToListAsync()
                : new List<Story>();

            var histories = userId > 0
                ? await _context.ReadingHistories
                    .Include(h => h.Story)
                    .Include(h => h.Chapter)
                    .Where(h => h.UserId == userId && h.Story != null && h.Chapter != null)
                    .OrderByDescending(h => h.UpdatedAt)
                    .ToListAsync()
                : new List<ReadingHistory>();

            var weekStart = ReaderMembershipService.VietnamWeekStart(System.DateTime.UtcNow);
            ViewBag.WeekCount = userId > 0 ? await _context.WeeklyChapterReads.CountAsync(r => r.UserId == userId && r.WeekStartDate == weekStart) : 0;
            ViewBag.Badges = userId > 0 ? await _context.BookwormBadges.AsNoTracking().Where(b => b.UserId == userId).OrderByDescending(b => b.WeekStartDate).ToListAsync() : new List<BookwormBadge>();
            ViewBag.Member = userId > 0 ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId) : null;
            ViewBag.CoinLog = userId > 0
                ? await _context.CoinLedgerEntries.AsNoTracking().Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync()
                : new List<CoinLedgerEntry>();
            ViewBag.UnlockedChapters = userId > 0
                ? await _context.CoinChapterUnlocks.CountAsync(x => x.UserId == userId) : 0;
            ViewBag.IsVip = userId > 0 && await ReaderMembershipService.IsVipAsync(_context, userId);
            ViewBag.Favorites = favorites;
            ViewBag.Histories = histories;

            return View();
        }
    }
}
