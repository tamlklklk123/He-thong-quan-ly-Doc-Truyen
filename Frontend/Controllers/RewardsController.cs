using System;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Models;
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    [Authorize]
    public class RewardsController : Controller
    {
        private readonly AppDbContext _db;
        public RewardsController(AppDbContext db) => _db = db;
        private int UserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetGoal(int goal)
        {
            if (UserId <= 0) return Unauthorized();
            if (goal < 5 || goal > 500)
                TempData["ErrorMessage"] = "Mục tiêu phải từ 5 đến 500 chương mỗi tuần.";
            else
            {
                var user = await _db.Users.FindAsync(UserId);
                if (user == null) return Unauthorized();
                user.WeeklyChapterGoal = goal;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã lưu mục tiêu đọc vào tài khoản.";
            }
            return RedirectToAction("Favorites", "Home");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Claim()
        {
            if (UserId <= 0) return Unauthorized();
            var week = ReaderMembershipService.VietnamWeekStart(DateTime.UtcNow);
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var user = await _db.Users.FindAsync(UserId);
            if (UserModeration.IsLoginBlocked(user)) return Unauthorized();
            if (await _db.BookwormBadges.AnyAsync(b => b.UserId == UserId && b.WeekStartDate == week))
                TempData["ErrorMessage"] = "Tuần này bạn đã nhận thưởng rồi.";
            else
            {
                var count = await _db.WeeklyChapterReads.CountAsync(r => r.UserId == UserId && r.WeekStartDate == week);
                if (count < user.WeeklyChapterGoal)
                    TempData["ErrorMessage"] = $"Bạn mới đọc {count}/{user.WeeklyChapterGoal} chương. Chưa đủ điều kiện nhận thưởng.";
                else
                {
                    _db.BookwormBadges.Add(new BookwormBadge
                    {
                        UserId = UserId, WeekStartDate = week, GoalAtClaim = user.WeeklyChapterGoal,
                        ChaptersRead = count, CoinsAwarded = 50
                    });
                    checked { user.CoinBalance += 50; }
                    _db.CoinLedgerEntries.Add(new CoinLedgerEntry
                    {
                        UserId = UserId, Delta = 50, BalanceAfter = user.CoinBalance,
                        Reason = "Thưởng huy hiệu Mọt Sách", Reference = $"bookworm:{week:yyyyMMdd}"
                    });
                    await _db.SaveChangesAsync();
                    TempData["SuccessMessage"] = "🎉 Nhận huy hiệu Mọt Sách và +50 Xu thành công!";
                }
            }
            await tx.CommitAsync();
            return RedirectToAction("Favorites", "Home");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RedeemTickets(int pack)
        {
            if (UserId <= 0) return Unauthorized();
            try { TempData["SuccessMessage"] = await ReaderPointsService.RedeemTicketsAsync(_db, UserId, pack); }
            catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
            return RedirectToAction("Favorites", "Home");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Equip(bool show)
        {
            if (UserId <= 0) return Unauthorized();
            var user = await _db.Users.FindAsync(UserId);
            if (user == null) return Unauthorized();
            user.ShowBookwormBadge = show;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = show ? "Đã trang bị huy hiệu Mọt Sách." : "Đã ẩn huy hiệu Mọt Sách cạnh tên.";
            return RedirectToAction("Profile", "Account");
        }
    }
}