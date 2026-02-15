using LibraryMPT.Data;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LibraryMPT.Api.Controllers;

[ApiController]
[Authorize(Roles = "Librarian")]
[Route("api/librarian")]
public sealed class LibrarianApiController : ControllerBase
{
    private readonly LibraryContext _context;

    public LibrarianApiController(LibraryContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<LibrarianDashboardResponse>> Dashboard()
    {
        var stats = _context.LibrarianStats
            .FromSqlRaw(@"
                SELECT
                    (SELECT COUNT(*) FROM Books) AS BooksCount,
                    (SELECT COUNT(*) FROM Categories) AS CategoriesCount,
                    (SELECT COUNT(*) FROM Users WHERE RoleID = 3) AS ActiveReadersCount,
                    (SELECT COUNT(*) FROM BookLogs
                     WHERE ActionAt >= DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0)) AS ActionsThisMonth
            ")
            .AsEnumerable()
            .FirstOrDefault() ?? new LibrarianStatsDto();

        var categoryStats = await _context.CategoryStats
            .FromSqlRaw(@"
                SELECT TOP 3
                    c.CategoryName AS CategoryName,
                    COUNT(b.BookID) AS BooksCount
                FROM Categories c
                LEFT JOIN Books b ON b.CategoryID = c.CategoryID
                GROUP BY c.CategoryName
                ORDER BY COUNT(b.BookID) DESC
            ")
            .AsNoTracking()
            .ToListAsync();

        var lastBooks = await _context.LastBooks
            .FromSqlRaw(@"
                SELECT TOP 3
                    b.Title AS Title,
                    CONCAT(a.FirstName, ' ', a.LastName) AS Author,
                    c.CategoryName AS Category
                FROM Books b
                INNER JOIN Authors a ON a.AuthorID = b.AuthorID
                INNER JOIN Categories c ON c.CategoryID = b.CategoryID
                ORDER BY b.BookID DESC
            ")
            .AsNoTracking()
            .ToListAsync();

        var pendingSubscriptionsCount = await _context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Subscriptions WHERE Status = 'Pending'")
            .SingleAsync();

        return Ok(new LibrarianDashboardResponse
        {
            Stats = stats,
            CategoryStats = categoryStats,
            LastBooks = lastBooks,
            PendingSubscriptionsCount = pendingSubscriptionsCount
        });
    }

    [HttpGet("books")]
    public async Task<ActionResult<LibrarianBookManagementResponse>> BookManagement(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] int? authorId,
        [FromQuery] bool? requiresSubscription)
    {
        var sql = """
            SELECT
                b.BookID,
                b.Title,
                b.AuthorID,
                b.ImagePath,
                b.Description,
                b.CategoryID,
                b.FilePath,
                b.PublishYear,
                b.PublisherID,
                b.RequiresSubscription
            FROM Books b
            WHERE 
                (@search IS NULL OR b.Title LIKE '%' + @search + '%')
                AND (@categoryId IS NULL OR b.CategoryID = @categoryId)
                AND (@authorId IS NULL OR b.AuthorID = @authorId)
                AND (@requiresSubscription IS NULL OR b.RequiresSubscription = @requiresSubscription)
            ORDER BY b.Title
        """;

        var books = await _context.Books
            .FromSqlRaw(sql,
                new SqlParameter("@search", (object?)search ?? DBNull.Value),
                new SqlParameter("@categoryId", (object?)categoryId ?? DBNull.Value),
                new SqlParameter("@authorId", (object?)authorId ?? DBNull.Value),
                new SqlParameter("@requiresSubscription", (object?)requiresSubscription ?? DBNull.Value))
            .AsNoTracking()
            .ToListAsync();

        var categories = await _context.Categories
            .FromSqlRaw("SELECT CategoryID, CategoryName FROM Categories ORDER BY CategoryName")
            .AsNoTracking()
            .ToListAsync();

        var authors = await _context.Authors
            .FromSqlRaw("SELECT AuthorID, FirstName, LastName FROM Authors ORDER BY LastName, FirstName")
            .AsNoTracking()
            .ToListAsync();

        return Ok(new LibrarianBookManagementResponse
        {
            Books = books,
            Categories = categories,
            Authors = authors
        });
    }

    [HttpGet("book/{id:int}")]
    public async Task<ActionResult<Book>> GetBook(int id)
    {
        var book = await _context.Books
            .FromSqlRaw("SELECT * FROM Books WHERE BookID = @id", new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return book is null ? NotFound() : Ok(book);
    }

    [HttpGet("book-lookups")]
    public async Task<ActionResult<LibrarianBookFormLookupsResponse>> BookLookups()
    {
        var categories = await _context.Categories
            .FromSqlRaw("SELECT CategoryID, CategoryName FROM Categories")
            .AsNoTracking()
            .ToListAsync();
        var authors = await _context.Authors
            .FromSqlRaw("SELECT AuthorID, FirstName, LastName FROM Authors")
            .AsNoTracking()
            .ToListAsync();
        var publishers = await _context.Publisher
            .FromSqlRaw("SELECT PublisherID, PublisherName FROM Publisher")
            .AsNoTracking()
            .ToListAsync();

        return Ok(new LibrarianBookFormLookupsResponse
        {
            Categories = categories,
            Authors = authors,
            Publishers = publishers
        });
    }

    [HttpPost("book")]
    public async Task<ActionResult<ApiCommandResponse>> AddBook([FromBody] Book book)
    {
        _context.Database.ExecuteSqlRaw(@"
            INSERT INTO Books (Title, Description, PublishYear, CategoryID, AuthorID, PublisherID, FilePath, RequiresSubscription)
            VALUES (@title, @description, @year, @categoryId, @authId, @pubId, @filePath, @requiresSubscription)",
            new SqlParameter("@title", book.Title.Trim()),
            new SqlParameter("@description", (object?)book.Description?.Trim() ?? DBNull.Value),
            new SqlParameter("@year", (object?)book.PublishYear ?? DBNull.Value),
            new SqlParameter("@categoryId", book.CategoryID),
            new SqlParameter("@authId", book.AuthorID),
            new SqlParameter("@pubId", (object?)book.PublisherID ?? DBNull.Value),
            new SqlParameter("@filePath", (object?)book.FilePath?.Trim() ?? DBNull.Value),
            new SqlParameter("@requiresSubscription", book.RequiresSubscription)
        );

        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("book/{id:int}")]
    public ActionResult<ApiCommandResponse> EditBook(int id, [FromBody] Book book)
    {
        if (id != book.BookID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Book id mismatch." });
        }

        _context.Database.ExecuteSqlRaw(@"
            UPDATE Books
            SET Title = @title,
                Description = @description,
                PublishYear = @year,
                CategoryID = @categoryId,
                AuthorID = @authId,
                PublisherID = @pubId,
                FilePath = @filePath,
                RequiresSubscription = @requiresSubscription
            WHERE BookID = @id",
            new SqlParameter("@title", book.Title.Trim()),
            new SqlParameter("@description", (object?)book.Description?.Trim() ?? DBNull.Value),
            new SqlParameter("@year", (object?)book.PublishYear ?? DBNull.Value),
            new SqlParameter("@categoryId", book.CategoryID),
            new SqlParameter("@authId", book.AuthorID),
            new SqlParameter("@pubId", (object?)book.PublisherID ?? DBNull.Value),
            new SqlParameter("@filePath", (object?)book.FilePath?.Trim() ?? DBNull.Value),
            new SqlParameter("@requiresSubscription", book.RequiresSubscription),
            new SqlParameter("@id", book.BookID)
        );

        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("book/{id:int}")]
    public ActionResult<ApiCommandResponse> DeleteBook(int id)
    {
        _context.Database.ExecuteSqlRaw("DELETE FROM Books WHERE BookID = @id", new SqlParameter("@id", id));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<Category>>> Categories([FromQuery] string? search)
    {
        var sql = @"
            SELECT CategoryID, CategoryName 
            FROM Categories 
            WHERE (@search IS NULL OR CategoryName LIKE '%' + @search + '%')
            ORDER BY CategoryName
        ";
        var categories = await _context.Categories
            .FromSqlRaw(sql, new SqlParameter("@search", (object?)search ?? DBNull.Value))
            .AsNoTracking()
            .ToListAsync();
        return Ok(categories);
    }

    [HttpGet("categories/{id:int}")]
    public async Task<ActionResult<Category>> CategoryById(int id)
    {
        var category = await _context.Categories
            .FromSqlRaw("SELECT * FROM Categories WHERE CategoryID = @id", new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return category is null ? NotFound() : Ok(category);
    }

    [HttpPost("categories")]
    public ActionResult<ApiCommandResponse> AddCategory([FromBody] Category category)
    {
        _context.Database.ExecuteSqlRaw(
            "INSERT INTO Categories (CategoryName) VALUES (@name)",
            new SqlParameter("@name", category.CategoryName.Trim())
        );
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("categories/{id:int}")]
    public ActionResult<ApiCommandResponse> EditCategory(int id, [FromBody] Category category)
    {
        if (id != category.CategoryID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Category id mismatch." });
        }

        _context.Database.ExecuteSqlRaw(
            "UPDATE Categories SET CategoryName = @name WHERE CategoryID = @id",
            new SqlParameter("@name", category.CategoryName.Trim()),
            new SqlParameter("@id", category.CategoryID)
        );
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("categories/{id:int}")]
    public ActionResult<ApiCommandResponse> DeleteCategory(int id)
    {
        _context.Database.ExecuteSqlRaw(
            "DELETE FROM Categories WHERE CategoryID = @id",
            new SqlParameter("@id", id)
        );
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("authors")]
    public async Task<ActionResult<List<Author>>> Authors([FromQuery] string? search)
    {
        var sql = @"
            SELECT AuthorID, FirstName, LastName 
            FROM Authors 
            WHERE (@search IS NULL OR FirstName LIKE '%' + @search + '%' OR LastName LIKE '%' + @search + '%')
            ORDER BY LastName, FirstName
        ";
        var authors = await _context.Authors
            .FromSqlRaw(sql, new SqlParameter("@search", (object?)search ?? DBNull.Value))
            .AsNoTracking()
            .ToListAsync();
        return Ok(authors);
    }

    [HttpGet("authors/{id:int}")]
    public async Task<ActionResult<Author>> AuthorById(int id)
    {
        var author = await _context.Authors
            .FromSqlRaw("SELECT * FROM Authors WHERE AuthorID = @id", new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return author is null ? NotFound() : Ok(author);
    }

    [HttpPost("authors")]
    public ActionResult<ApiCommandResponse> AddAuthor([FromBody] Author author)
    {
        _context.Database.ExecuteSqlRaw(
            "INSERT INTO Authors (FirstName, LastName) VALUES (@firstName, @lastName)",
            new SqlParameter("@firstName", author.FirstName.Trim()),
            new SqlParameter("@lastName", author.LastName.Trim())
        );
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("authors/{id:int}")]
    public ActionResult<ApiCommandResponse> EditAuthor(int id, [FromBody] Author author)
    {
        if (id != author.AuthorID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Author id mismatch." });
        }

        _context.Database.ExecuteSqlRaw(
            "UPDATE Authors SET FirstName = @firstName, LastName = @lastName WHERE AuthorID = @id",
            new SqlParameter("@firstName", author.FirstName.Trim()),
            new SqlParameter("@lastName", author.LastName.Trim()),
            new SqlParameter("@id", author.AuthorID)
        );
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("authors/{id:int}")]
    public ActionResult<ApiCommandResponse> DeleteAuthor(int id)
    {
        _context.Database.ExecuteSqlRaw(
            "DELETE FROM Authors WHERE AuthorID = @id",
            new SqlParameter("@id", id)
        );
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("publishers")]
    public async Task<ActionResult<List<Publisher>>> Publishers([FromQuery] string? search)
    {
        var sql = @"
            SELECT PublisherID, PublisherName 
            FROM Publisher 
            WHERE (@search IS NULL OR PublisherName LIKE '%' + @search + '%')
            ORDER BY PublisherName
        ";
        var publishers = await _context.Publisher
            .FromSqlRaw(sql, new SqlParameter("@search", (object?)search ?? DBNull.Value))
            .AsNoTracking()
            .ToListAsync();
        return Ok(publishers);
    }

    [HttpGet("publishers/{id:int}")]
    public async Task<ActionResult<Publisher>> PublisherById(int id)
    {
        var publisher = await _context.Publisher
            .FromSqlRaw("SELECT * FROM Publisher WHERE PublisherID = @id", new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return publisher is null ? NotFound() : Ok(publisher);
    }

    [HttpPost("publishers")]
    public ActionResult<ApiCommandResponse> AddPublisher([FromBody] Publisher publisher)
    {
        _context.Database.ExecuteSqlRaw(
            "INSERT INTO Publisher (PublisherName) VALUES (@name)",
            new SqlParameter("@name", publisher.PublisherName.Trim())
        );
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("publishers/{id:int}")]
    public ActionResult<ApiCommandResponse> EditPublisher(int id, [FromBody] Publisher publisher)
    {
        if (id != publisher.PublisherID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Publisher id mismatch." });
        }

        _context.Database.ExecuteSqlRaw(
            "UPDATE Publisher SET PublisherName = @name WHERE PublisherID = @id",
            new SqlParameter("@name", publisher.PublisherName.Trim()),
            new SqlParameter("@id", publisher.PublisherID)
        );
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("publishers/{id:int}")]
    public ActionResult<ApiCommandResponse> DeletePublisher(int id)
    {
        _context.Database.ExecuteSqlRaw(
            "DELETE FROM Publisher WHERE PublisherID = @id",
            new SqlParameter("@id", id)
        );
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("subscription-requests")]
    public async Task<ActionResult<SubscriptionRequestsResponse>> SubscriptionRequests([FromQuery] string? status)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
        object statusParam = normalizedStatus switch
        {
            "pending" => "Pending",
            "approved" => "Approved",
            "rejected" => "Rejected",
            _ => DBNull.Value
        };

        var requests = await _context.SubscriptionRequests
            .FromSqlRaw("""
                SELECT
                    s.SubscriptionID,
                    s.FacultyID,
                    s.Name AS SubscriptionName,
                    s.DurationDays,
                    s.Status,
                    s.RequestedByUserID,
                    u.Username AS RequestedByUsername,
                    u.Email AS RequestedByEmail,
                    f.FacultyName,
                    (SELECT COUNT(*) FROM Users u2
                     JOIN Roles r ON r.RoleID = u2.RoleID
                     WHERE u2.FacultyID = s.FacultyID AND r.RoleName = 'Student') AS StudentsCount
                FROM Subscriptions s
                LEFT JOIN Users u ON u.UserID = s.RequestedByUserID
                LEFT JOIN Faculty f ON f.FacultyID = s.FacultyID
                WHERE s.RequestedByUserID IS NOT NULL
                  AND (@status IS NULL OR s.Status = @status)
                ORDER BY
                    CASE WHEN s.Status = 'Pending' THEN 1
                         WHEN s.Status = 'Rejected' THEN 2
                         WHEN s.Status = 'Approved' THEN 3
                         ELSE 4 END,
                    CASE WHEN s.Status IN ('Pending', 'Rejected') THEN s.SubscriptionID END ASC,
                    s.SubscriptionID DESC
            """, new SqlParameter("@status", statusParam))
            .AsNoTracking()
            .ToListAsync();

        return Ok(new SubscriptionRequestsResponse
        {
            StatusFilter = normalizedStatus,
            Requests = requests
        });
    }

    [HttpPost("subscription-requests/{subscriptionId:int}/approve")]
    public async Task<ActionResult<ApiCommandResponse>> ApproveSubscription(int subscriptionId)
    {
        var subscription = await _context.Subscriptions
            .FromSqlRaw("""
                SELECT * FROM Subscriptions
                WHERE SubscriptionID = @id AND Status = 'Pending'
            """, new SqlParameter("@id", subscriptionId))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (subscription == null || !subscription.DurationDays.HasValue)
        {
            return BadRequest(new ApiCommandResponse
            {
                Success = false,
                Message = "Заявка не найдена или уже обработана."
            });
        }

        var startDate = DateTime.Today;
        var endDate = startDate.AddDays(subscription.DurationDays.Value);

        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Subscriptions
            SET Status = 'Approved',
                StartDate = @startDate,
                EndDate = @endDate
            WHERE SubscriptionID = @id
        """,
            new SqlParameter("@startDate", startDate),
            new SqlParameter("@endDate", endDate),
            new SqlParameter("@id", subscriptionId));

        return Ok(new ApiCommandResponse
        {
            Success = true,
            Message = $"Подписка \"{subscription.Name}\" одобрена и активна с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}."
        });
    }

    [HttpPost("subscription-requests/{subscriptionId:int}/reject")]
    public async Task<ActionResult<ApiCommandResponse>> RejectSubscription(int subscriptionId)
    {
        var subscription = await _context.Subscriptions
            .FromSqlRaw("""
                SELECT * FROM Subscriptions
                WHERE SubscriptionID = @id AND Status = 'Pending'
            """, new SqlParameter("@id", subscriptionId))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            return BadRequest(new ApiCommandResponse
            {
                Success = false,
                Message = "Заявка не найдена или уже обработана."
            });
        }

        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Subscriptions
            SET Status = 'Rejected'
            WHERE SubscriptionID = @id
        """, new SqlParameter("@id", subscriptionId));

        return Ok(new ApiCommandResponse
        {
            Success = true,
            Message = $"Заявка на подписку \"{subscription.Name}\" отклонена."
        });
    }
}

