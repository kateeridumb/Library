namespace LibraryMPT.Models
{
    public class StudentStatsDto
    {
        public int UserID { get; set; }
        public string Username { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public int TotalDownloads { get; set; }
        public int TotalReads { get; set; }
        public int TotalBooksRead { get; set; }
        public DateTime? LastActivityDate { get; set; }
    }
}

