using System.ComponentModel.DataAnnotations;

namespace LibraryMPT.Models
{
    public class BookmarkDto
    {
        public int BookmarkID { get; set; }
        
        [Required]
        public int BookID { get; set; }
        
        public string? Page { get; set; }
        public string? Position { get; set; }
        public string? Title { get; set; }
        public string? Note { get; set; }
    }
}

