namespace LibraryMPT.Models
{
    public class Bookmark
    {
        public int BookmarkID { get; set; }
        public int UserID { get; set; }
        public int BookID { get; set; }
        public string? Page { get; set; }
        public string? Position { get; set; }
        public string? Title { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

