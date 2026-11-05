using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using ServerCSharp.Data;
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    public class BillingController : Controller
    {
        private readonly AppDbContext _db;
        public BillingController(AppDbContext db) => _db = db;
        private int CurrentUserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public async Task<IActionResult> Index(int? storyId, int? chapterId)
        {
            var story = storyId.HasValue ? await _db.Stories.FindAsync(storyId.Value) : null;
            ViewBag.Story = story;
            var lockedChapter = chapterId.HasValue ? await _db.Chapters.Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == chapterId.Value && c.Story != null && c.Story.ApprovalStatus == "approved") : null;
            ViewBag.LockedChapter = lockedChapter != null && lockedChapter.Story != null &&
                ReadingAccessService.IsVisible(lockedChapter) && !ReadingAccessService.IsFree(lockedChapter.Story, lockedChapter)
                ? lockedChapter : null;
            var uid = CurrentUserId;
            if (uid > 0)
            {
                ViewBag.Orders = await _db.PaymentTransactions.AsNoTracking().Where(x => x.UserId == uid)
                    .OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync();
                ViewBag.Subscriptions = await _db.UserSubscriptions.Include(x => x.Plan).AsNoTracking()
                    .Where(x => x.UserId == uid && x.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(x => x.ExpiresAt).ToListAsync();
                var wallet = await _db.Users.Where(u => u.Id == uid).Select(u => new { u.CoinBalance, u.LoyaltyPoints, u.ChapterTickets }).FirstOrDefaultAsync();
                ViewBag.CoinBalance = wallet?.CoinBalance ?? 0;
                ViewBag.LoyaltyPoints = wallet?.LoyaltyPoints ?? 0;
                ViewBag.ChapterTickets = wallet?.ChapterTickets ?? 0;
                ViewBag.HasTopUp = await _db.PaymentTransactions.AnyAsync(t => t.UserId == uid && t.Type == "coins" && t.Status == "paid" && t.CoinAmount > 0);
                ViewBag.IsVip = await ReaderMembershipService.IsVipAsync(_db, uid);
                ViewBag.Purchases = await _db.StoryPurchases.Include(x => x.Story).AsNoTracking()
                    .Where(x => x.UserId == uid).ToListAsync();
            }
            ViewBag.RecentUnlocks = uid > 0 ? await _db.CoinChapterUnlocks.Include(x => x.Chapter).AsNoTracking()
                .Where(x => x.UserId == uid).OrderByDescending(x => x.UnlockedAt).Take(10).ToListAsync()
                : new System.Collections.Generic.List<ServerCSharp.Models.CoinChapterUnlock>();
            return View(await _db.SubscriptionPlans.Where(x => x.IsActive).OrderBy(x => x.Price).ToListAsync());
        }

        [Authorize]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> PlaceOrder(string type, int targetId, int? storyId)
        {
            try
            {
                var order = await BillingService.CreateOrderAsync(_db, CurrentUserId, type, targetId);
                TempData["SuccessMessage"] = $"Đã tạo đơn {order.OrderCode}. Chờ Admin kiểm tra thanh toán và xác nhận. Không tự động cấp VIP hoặc cộng Xu.";
            }
            catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sql && sql.Number == 547 &&
                sql.Message.Contains("CK_PaymentTransactions_Type", StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] = "CSDL chưa hỗ trợ giao dịch nạp Xu. Vui lòng chạy SQL/fix_payment_transaction_type.sql trên WebDocTruyenDb rồi thử lại.";
            }
            return RedirectToAction("Index", new { storyId });
        }
    }
}
