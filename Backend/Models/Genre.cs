using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ServerCSharp.Models
{
    public class Genre
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(10)]
        public string? Icon { get; set; } = "📖";

        [JsonIgnore]
        public List<StoryGenre> StoryGenres { get; set; } = new();
    }
}
