namespace LibraryMPT.Models
{
    public class BookDownloadDto
    {
        public int BookID { get; set; }
        public string? FilePath { get; set; }
        public string Title { get; set; } = null!;
        public bool RequiresSubscription { get; set; }
    }

}
