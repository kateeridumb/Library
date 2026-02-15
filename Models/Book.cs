namespace LibraryMPT.Models
{
    public class Book
    {
        public int BookID { get; set; }
        public string Title { get; set; }
        public string? ImagePath { get; set; }
        public string? Description { get; set; }
        public int? PublishYear { get; set; }
        public int CategoryID { get; set; }
        public int AuthorID { get; set; }
        public int? PublisherID { get; set; }


        public string? FilePath { get; set; }
        public bool RequiresSubscription { get; set; }
        public Category? Category { get; set; }
        public Author? Author { get; set; }
        public Publisher? Publisher { get; set; }
    }
}