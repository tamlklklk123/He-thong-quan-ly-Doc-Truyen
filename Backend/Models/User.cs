using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ServerCSharp.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [JsonIgnore]
        public string Password { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Role { get; set; } = "reader"; // 'admin', 'author', 'reader'

        [MaxLength(20)]
        public string Status { get; set; } = "active"; // 'active', 'banned'

        public string? Avatar { get; set; }
        public string? Bio { get; set; }

        // active | restricted (cannot post/comment/rate/report) | suspended (cannot sign in) | banned.
        // When StatusUntil has elapsed, access is restored logically even before manual cleanup.
        public DateTime? StatusUntil { get; set; }
        [MaxLength(500)] public string? ModerationReason { get; set; }
        public DateTime? StatusChangedAt { get; set; }
        public int? StatusChangedByAdminId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Số dư Xu lưu SQL Server. Tài khoản mới mặc định 0 Xu và không phải VIP.
        public int CoinBalance { get; set; } = 0;
        // Điểm kinh nghiệm không tiêu được, chỉ tăng khi đọc chương lần đầu.
        public int LoyaltyPoints { get; set; } = 0;
        // Vé mở khóa chương mua bằng Xu; lưu vĩnh viễn theo tài khoản.
        public int ChapterTickets { get; set; } = 0;
        public int WeeklyChapterGoal { get; set; } = 24;
        public bool ShowBookwormBadge { get; set; } = true;

        // Navigation properties
        [JsonIgnore]
        public List<Story> Stories { get; set; } = new();

        [JsonIgnore]
        public List<Favorite> Favorites { get; set; } = new();

        [JsonIgnore]
        public List<ReadingHistory> ReadingHistories { get; set; } = new();

        [JsonIgnore]
        public List<Comment> Comments { get; set; } = new();

        [JsonIgnore]
        public List<Report> Reports { get; set; } = new();

        [JsonIgnore]
        public List<StoryRating> Ratings { get; set; } = new();
    }
}
