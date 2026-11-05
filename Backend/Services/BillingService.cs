using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Models;

namespace ServerCSharp.Services
{
    // Demo thanh toán: tạo đơn CHỜ XÁC NHẬN. Chỉ Admin có quyền xác nhận sau khi
    // đối soát tiền thực tế. Không có cổng thanh toán thật / webhook trong demo.
    public static class BillingService
    {
        public static async Task<PaymentTransaction> CreateOrderAsync(AppDbContext db, int userId, string type, int targetId)
        {
            if (UserModeration.IsLoginBlocked(await db.Users.FindAsync(userId)))
                throw new InvalidOperationException("Tài khoản không hợp lệ.");
            decimal amount;
            int? planId = null, storyId = null, coinAmount = null;
            if (type == "plan")
            {
                var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == targetId && p.IsActive);
                if (plan == null || plan.Price < 0 || plan.DurationDays < 1)
                    throw new InvalidOperationException("Gói đọc không hợp lệ hoặc đã ngừng bán.");
                amount = plan.Price;
                planId = plan.Id;
            }
            else if (type == "story")
            {
                var story = await db.Stories.FirstOrDefaultAsync(s => s.Id == targetId && s.ApprovalStatus == "approved");
                if (story == null || story.AccessType == "free" || story.Price <= 0)
                    throw new InvalidOperationException("Truyện không có giá mua hợp lệ.");
                if (await db.StoryPurchases.AnyAsync(p => p.StoryId == targetId && p.UserId == userId))
                    throw new InvalidOperationException("Bạn đã sở hữu quyền đọc truyện này.");
                amount = story.Price;
                storyId = story.Id;
            }
            else if (type == "coins")
            {
                // Gói nạp cố định: không tin giá hoặc số Xu do client gửi.
                (amount, coinAmount) = targetId switch
                {
                    1 => (20000m, 200),
                    2 => (50000m, 550),
                    3 => (100000m, 1200),
                    _ => throw new InvalidOperationException("Gói nạp Xu không hợp lệ.")
                };
            }
            else throw new InvalidOperationException("Loại giao dịch không hợp lệ.");

            var existing = await db.PaymentTransactions.OrderByDescending(p => p.Id).FirstOrDefaultAsync(p =>
                p.UserId == userId && p.Type == type && p.PlanId == planId && p.StoryId == storyId && p.CoinAmount == coinAmount &&
                p.Status == "pending" && p.Amount == amount);
            if (existing != null) return existing;

            var order = new PaymentTransaction
            {
                UserId = userId, Type = type, PlanId = planId, StoryId = storyId, CoinAmount = coinAmount,
                Amount = amount, OrderCode = "MT-" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
                Status = "pending", CreatedAt = DateTime.UtcNow
            };
            db.PaymentTransactions.Add(order);
            await db.SaveChangesAsync();
            return order;
        }

        public static async Task<PaymentTransaction> SettleAsync(AppDbContext db, int orderId, int adminId, bool success, string? note = null)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await db.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == orderId);
            if (order == null) throw new InvalidOperationException("Không tìm thấy giao dịch.");
            if (order.Status != "pending") throw new InvalidOperationException("Giao dịch đã được xử lý.");
            if (UserModeration.IsLoginBlocked(await db.Users.FindAsync(order.UserId)))
                throw new InvalidOperationException("Tài khoản không còn hoạt động.");

            if (success)
            {
                if (order.Type == "plan" && order.PlanId.HasValue)
                {
                    var plan = await db.SubscriptionPlans.FindAsync(order.PlanId.Value);
                    if (plan == null || plan.DurationDays <= 0)
                        throw new InvalidOperationException("Gói đã không còn tồn tại.");
                    var now = DateTime.UtcNow;
                    var expiry = await db.UserSubscriptions
                        .Where(s => s.UserId == order.UserId && s.PlanId == plan.Id)
                        .MaxAsync(s => (DateTime?)s.ExpiresAt);
                    var start = expiry.HasValue && expiry.Value > now ? expiry.Value : now;
                    db.UserSubscriptions.Add(new UserSubscription
                    {
                        UserId = order.UserId, PlanId = plan.Id,
                        PaymentTransactionId = order.Id, StartsAt = start, ExpiresAt = start.AddDays(plan.DurationDays)
                    });
                }
                else if (order.Type == "story" && order.StoryId.HasValue)
                {
                    if (await db.StoryPurchases.AnyAsync(x => x.UserId == order.UserId && x.StoryId == order.StoryId.Value))
                        throw new InvalidOperationException("Thành viên đã mua truyện; cần đối soát hoàn tiền thay vì xác nhận trùng.");
                    db.StoryPurchases.Add(new StoryPurchase
                    {
                        UserId = order.UserId, StoryId = order.StoryId.Value,
                        PaymentTransactionId = order.Id, PurchasedAt = DateTime.UtcNow
                    });
                }
                else if (order.Type == "coins" && order.CoinAmount is > 0)
                {
                    var member = await db.Users.FirstAsync(u => u.Id == order.UserId);
                    checked { member.CoinBalance += order.CoinAmount.Value; }
                    db.CoinLedgerEntries.Add(new CoinLedgerEntry
                    {
                        UserId = order.UserId, Delta = order.CoinAmount.Value, BalanceAfter = member.CoinBalance,
                        Reason = "Nạp Xu được Admin xác nhận", Reference = $"topup:{order.Id}"
                    });
                }
                else throw new InvalidOperationException("Giao dịch thiếu thông tin đích.");
                order.Status = "paid";
                order.PaidAt = DateTime.UtcNow;
            }
            else order.Status = "failed";
            order.ConfirmedByAdminId = adminId;
            order.AdminNote = note?.Trim();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return order;
        }
    }
}
