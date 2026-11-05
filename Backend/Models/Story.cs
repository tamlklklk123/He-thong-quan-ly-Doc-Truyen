using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ServerCSharp.Models
{
    public class Story
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Slug { get; set; } = string.Empty;

        public int AuthorId { get; set; }

        [ForeignKey("AuthorId")]
        public User? Author { get; set; }

        [MaxLength(100)]
        public string AuthorName { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string? CoverUrl { get; set; }

        [MaxLength(20)]
        public string Type { get; set; } = "novel"; // 'novel' or 'comic'

        [MaxLength(20)]
        public string Status { get; set; } = "ongoing"; // 'ongoing' or 'completed'

        [MaxLength(20)]
        public string ApprovalStatus { get; set; } = "pending"; // 'pending', 'approved', 'rejected'

        public string? RejectionReason { get; set; }

        // Chính sách thu phí: free / paid / mixed. Mặc định miễn phí để giữ dữ liệu cũ.
        [MaxLength(10)] public string AccessType { get; set; } = "free";
        [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; } = 0;
        public bool AllowSubscription { get; set; } = true;

        public int Views { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [JsonIgnore]
        public List<StoryGenre> StoryGenres { get; set; } = new();

        [JsonIgnore]
        public List<Chapter> Chapters { get; set; } = new();

        [JsonIgnore]
        public List<Favorite> Favorites { get; set; } = new();

        [JsonIgnore]
        public List<ReadingHistory> ReadingHistories { get; set; } = new();

        [JsonIgnore]
        public List<Comment> Comments { get; set; } = new();

        [JsonIgnore]
        public List<StoryRating> Ratings { get; set; } = new();
    }
}
