namespace LibraryMPT.Models;

public sealed class ClientIndexResponse
{
    public List<Book> Books { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public bool HasSubscription { get; set; }
    public string SubscriptionStatus { get; set; } = "Нет активной подписки";
    public List<int> ReadedBookIds { get; set; } = new();
    public int TotalBooks { get; set; }
    public int Readed { get; set; }
}

public sealed class ClientBookDetailsResponse
{
    public Book? Book { get; set; }
    public bool HasSubscription { get; set; }
}

public sealed class ClientReadOnlineResponse
{
    public Book? Book { get; set; }
    public bool HasSubscription { get; set; }
    public bool CanRead { get; set; }
    public string? FilePath { get; set; }
    public string FileType { get; set; } = "unknown";
}

public sealed class ClientReadedResponse
{
    public List<Book> Books { get; set; } = new();
}

public sealed class MarkAsReadRequest
{
    public int UserId { get; set; }
    public int BookId { get; set; }
}

public sealed class ClientBookmarkRequest
{
    public int UserId { get; set; }
    public BookmarkDto Bookmark { get; set; } = new();
}

