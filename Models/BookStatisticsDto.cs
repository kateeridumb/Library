namespace LibraryMPT.Models
{
    public class BookStatisticsDto
    {
        public int BookID { get; set; }
        public string Title { get; set; } = null!;
        public int TotalDownloads { get; set; }
        public int TotalReads { get; set; }
        public int UniqueDownloaders { get; set; }
    }
}

