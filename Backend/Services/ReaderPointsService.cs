using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Models;

namespace ServerCSharp.Services
{
    public static class ReaderPointsService
    {
        public const int PointsPerUniqueChapter = 10;
        public const int ChapterCoinPrice = 20;
        public static string Rank(int points) => points >= 5000 ? "Đại Thần" : points >= 2000 ? "Cao Thủ Đọc Truyện" : points >= 500 ? "Mọt Sách" : "Độc Giả Mới";
        public static int Level(int points) => points >= 5000 ? 4 : points >= 2000 ? 3 : points >= 500 ? 2 : 1;
        public static int NextLevelAt(int points) => points >= 5000 ? 5000 : points >= 2000 ? 5000 : points >= 500 ? 2000 : 500;

        // Mọi giao dịch tiêu Xu được khóa theo Serializable, kiểm tra số dư và quyền sở hữu ở server.
        public static async Task<string> UnlockAsync(AppDbContext db, int userId, int chapterId, bool useTicket)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var member = await db.Users.FindAsync(userId);
            if (UserModeration.IsLoginBlocked(member)) throw new InvalidOperationException("Vui lòng đăng nhập tài khoản hợp lệ.");
            var chapter = await db.Chapters.Include(c => c.Story).FirstOrDefaultAsync(c => c.Id == chapterId);
            if (chapter?.Story == null || chapter.Story.ApprovalStatus != "approved" || !ReadingAccessService.IsVisible(chapter))
                throw new InvalidOperationException("Chương chưa được phát hành.");
            if (await ReadingAccessService.CanReadAsync(db, chapter.Story, chapter, userId))
                return "Bạn đã có quyền đọc chương này, không cần trả Xu.";
            if (await db.CoinChapterUnlocks.AnyAsync(x => x.UserId == userId && x.ChapterId == chapterId))
                return "Chương này đã được mở khóa.";
            if (useTicket)
            {
                if (member.ChapterTickets < 1) throw new InvalidOperationException("Bạn chưa có vé mở chương. Vào Tủ Truyện để đổi vé bằng Xu.");
                member.ChapterTickets -= 1;
            }
            else
            {
                if (member.CoinBalance < ChapterCoinPrice) throw new InvalidOperationException($"Không đủ Xu. Cần {ChapterCoinPrice} Xu để mở chương.");
                member.CoinBalance -= ChapterCoinPrice;
                db.CoinLedgerEntries.Add(new CoinLedgerEntry { UserId = userId, Delta = -ChapterCoinPrice, BalanceAfter = member.CoinBalance,
                    Reason = "Mở khóa chương", Reference = $"chapter:{chapterId}" });
            }
            db.CoinChapterUnlocks.Add(new CoinChapterUnlock
            {
                UserId = userId, ChapterId = chapterId, CoinsSpent = useTicket ? 0 : ChapterCoinPrice, UsedTicket = useTicket
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return useTicket ? "Đã dùng 1 vé để mở chương vĩnh viễn!" : $"Đã dùng {ChapterCoinPrice} Xu để mở chương vĩnh viễn!";
        }

        public static async Task<string> RedeemTicketsAsync(AppDbContext db, int userId, int pack)
        {
            if (pack != 1 && pack != 5) throw new InvalidOperationException("Gói vé không hợp lệ.");
            int cost = pack == 5 ? 80 : 18;
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var member = await db.Users.FindAsync(userId);
            if (UserModeration.IsLoginBlocked(member)) throw new InvalidOperationException("Tài khoản không hợp lệ.");
            if (member.CoinBalance < cost) throw new InvalidOperationException($"Không đủ {cost} Xu để đổi vé.");
            checked { member.ChapterTickets += pack; }
            member.CoinBalance -= cost;
            db.CoinLedgerEntries.Add(new CoinLedgerEntry { UserId = userId, Delta = -cost, BalanceAfter = member.CoinBalance,
                Reason = $"Đổi {pack} vé mở chương", Reference = $"tickets:{Guid.NewGuid():N}" });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return $"Đã đổi thành công {pack} vé mở chương. Số vé hiện có: {member.ChapterTickets}.";
        }
    }
}
