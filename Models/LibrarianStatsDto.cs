namespace LibraryMPT.Models
{
    public class LibrarianStatsDto
    {
        public int BooksCount { get; set; }
        public int CategoriesCount { get; set; }
        public int ActiveReadersCount { get; set; }
        public int ActionsThisMonth { get; set; }
    }
    public class CategoryStatDto
    {
        public string CategoryName { get; set; }
        public int BooksCount { get; set; }
    }
    public class LastBookDto
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Category { get; set; }
    }


}
