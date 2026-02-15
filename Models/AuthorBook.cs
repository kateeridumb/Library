namespace LibraryMPT.Models
{
    public class AuthorBook
    {
        public int AuthorBookID { get; set; }
        public int AuthorID { get; set; }
        public int BookID { get; set; }

        public Author Author { get; set; }
        public Book Book { get; set; }
    }
}
