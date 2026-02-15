namespace LibraryMPT.Models
{
    public class BookLog
    {
        public int LogID { get; set; }

        public int UserID { get; set; }
        public int BookID { get; set; }

        public string ActionType { get; set; } = null!;
        public DateTime ActionAt { get; set; }

        public User User { get; set; } = null!;
        public Book Book { get; set; } = null!;
    }
}
