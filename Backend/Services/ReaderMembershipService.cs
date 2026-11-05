using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;

namespace ServerCSharp.Services
{
    public static class ReaderMembershipService
    {
        // Có gói còn hạn VÀ ít nhất một giao dịch nạp Xu được Admin xác nhận.
        public static async Task<bool> IsVipAsync(AppDbContext db, int userId)
        {
            if (userId <= 0) return false;
            var now = DateTime.UtcNow;
            return await db.UserSubscriptions.AnyAsync(s => s.UserId == userId && s.StartsAt <= now && s.ExpiresAt > now)
                && await db.PaymentTransactions.AnyAsync(t => t.UserId == userId && t.Type == "coins" && t.Status == "paid" && t.CoinAmount > 0);
        }

        public static DateTime VietnamWeekStart(DateTime utcNow)
        {
            var local = utcNow.AddHours(7).Date;
            return local.AddDays(-((int)local.DayOfWeek + 6) % 7);
        }
    }
}