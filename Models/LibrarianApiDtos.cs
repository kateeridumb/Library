namespace LibraryMPT.Models;

public sealed class LibrarianDashboardResponse
{
    public LibrarianStatsDto Stats { get; set; } = new();
    public List<CategoryStatDto> CategoryStats { get; set; } = new();
    public List<LastBookDto> LastBooks { get; set; } = new();
    public int PendingSubscriptionsCount { get; set; }
}

public sealed class LibrarianBookManagementResponse
{
    public List<Book> Books { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Author> Authors { get; set; } = new();
}

public sealed class LibrarianBookFormLookupsResponse
{
    public List<Category> Categories { get; set; } = new();
    public List<Author> Authors { get; set; } = new();
    public List<Publisher> Publishers { get; set; } = new();
}

public sealed class SubscriptionRequestsResponse
{
    public string StatusFilter { get; set; } = "all";
    public List<SubscriptionRequestDto> Requests { get; set; } = new();
}

public sealed class ApiCommandResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

