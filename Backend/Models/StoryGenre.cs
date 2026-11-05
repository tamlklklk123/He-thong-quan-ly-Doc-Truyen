using System.ComponentModel.DataAnnotations.Schema;

namespace ServerCSharp.Models
{
    public class StoryGenre
    {
        public int StoryId { get; set; }

        [ForeignKey("StoryId")]
        public Story? Story { get; set; }

        public int GenreId { get; set; }

        [ForeignKey("GenreId")]
        public Genre? Genre { get; set; }
    }
}
