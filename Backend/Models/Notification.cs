using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServerCSharp.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Type { get; set; } = "system"; // system, chapter, story, comment

        [MaxLength(255)]
        public string? ActionUrl { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
