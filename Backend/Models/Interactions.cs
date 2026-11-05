using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServerCSharp.Models
{
    public class Favorite
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int StoryId { get; set; }

        [ForeignKey("StoryId")]
        public Story? Story { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ReadingHistory
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int StoryId { get; set; }

        [ForeignKey("StoryId")]
        public Story? Story { get; set; }

        public int ChapterId { get; set; }

        [ForeignKey("ChapterId")]
        public Chapter? Chapter { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Comment
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int StoryId { get; set; }

        [ForeignKey("StoryId")]
        public Story? Story { get; set; }

        public int? ChapterId { get; set; }

        [ForeignKey("ChapterId")]
        public Chapter? Chapter { get; set; }

        public string Content { get; set; } = string.Empty;
        public bool IsHidden { get; set; } = false;
        [System.ComponentModel.DataAnnotations.MaxLength(500)] public string? HiddenReason { get; set; }
        public DateTime? HiddenAt { get; set; }
        public int? HiddenByAdminId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Report
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        public string TargetType { get; set; } = string.Empty; // 'story', 'chapter', 'comment'

        public int TargetId { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string? Details { get; set; }

        public string Status { get; set; } = "pending"; // 'pending', 'resolved', 'dismissed'

        public string? ResolutionNotes { get; set; }
        public int? ResolvedByAdminId { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
