using System;
using System.ComponentModel.DataAnnotations;
namespace ServerCSharp.Models
{
    public class ModerationLog
    {
        public int Id { get; set; }
        public int AdminId { get; set; }
        [MaxLength(30)] public string TargetType { get; set; } = string.Empty;
        public int TargetId { get; set; }
        [MaxLength(40)] public string Action { get; set; } = string.Empty;
        [MaxLength(500)] public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
