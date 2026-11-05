using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServerCSharp.Models
{
    public class StoryRating
    {
        public int Id { get; set; }

        public int StoryId { get; set; }
        [ForeignKey(nameof(StoryId))]
        public Story? Story { get; set; }

        public int UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [Range(1, 5)]
        public int Score { get; set; }

        [MaxLength(1500)]
        public string? Review { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
