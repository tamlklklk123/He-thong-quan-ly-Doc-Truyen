using System;

namespace ServerCSharp.Models
{
    // Lưu mỗi chương đã mở một lần trong một tuần (giờ Việt Nam), tránh spam refresh để kiếm Xu.
    public class WeeklyChapterRead
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ChapterId { get; set; }
        public DateTime WeekStartDate { get; set; }
        public DateTime ReadAt { get; set; } = DateTime.UtcNow;
        public User? User { get; set; }
        public Chapter? Chapter { get; set; }
    }
    // Một hàng duy nhất cho mỗi chương thực sự đã đọc, chống cộng XP qua refresh.
    public class ChapterReadCredit
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ChapterId { get; set; }
        public int PointsAwarded { get; set; } = 10;
        public DateTime FirstReadAt { get; set; } = DateTime.UtcNow;
        public User? User { get; set; }
        public Chapter? Chapter { get; set; }
    }
    // Quyền sở hữu từng chương vĩnh viễn; có thể mua bằng Xu hoặc dùng vé.
    public class CoinChapterUnlock
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ChapterId { get; set; }
        public int CoinsSpent { get; set; }
        public bool UsedTicket { get; set; }
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
        public User? User { get; set; }
        public Chapter? Chapter { get; set; }
    }
    // Sổ biến động Xu, không cho sửa số dư bằng request từ client.
    public class CoinLedgerEntry
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int Delta { get; set; }
        public int BalanceAfter { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User? User { get; set; }
    }
    // Một hàng = một huy hiệu Mọt Sách mỗi tuần và một lần cộng 50 Xu.
    public class BookwormBadge
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime WeekStartDate { get; set; }
        public int GoalAtClaim { get; set; }
        public int ChaptersRead { get; set; }
        public int CoinsAwarded { get; set; } = 50;
        public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
        public User? User { get; set; }
    }
}