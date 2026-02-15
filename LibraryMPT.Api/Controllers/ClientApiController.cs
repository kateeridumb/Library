using LibraryMPT.Data;
using LibraryMPT.Api.Extensions;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LibraryMPT.Api.Controllers;

[ApiController]
[Authorize(Roles = "Student,Admin,Librarian,InstitutionRepresentative,Guest")]
[Route("api/client")]
public sealed class ClientApiController : ControllerBase
{
    private readonly LibraryContext _context;
    private readonly ILogger<ClientApiController> _logger;
    private readonly IWebHostEnvironment _environment;

    public ClientApiController(
        LibraryContext context,
        ILogger<ClientApiController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    [HttpGet("index")]
    public async Task<ActionResult<ClientIndexResponse>> Index([FromQuery] string? search, [FromQuery] int? categoryId)
    {
        var userId = User.GetUserId();
        var sql = """
            SELECT *
            FROM Books
            WHERE 1 = 1
        """;

        if (!string.IsNullOrWhiteSpace(search))
            sql += " AND (Title LIKE '%' + {0} + '%' OR Description LIKE '%' + {0} + '%')";

        if (categoryId.HasValue)
            sql += " AND CategoryID = {1}";

        var books = await _context.Books
            .FromSqlRaw(sql, search ?? (object)DBNull.Value, categoryId ?? (object)DBNull.Value)
            .AsNoTracking()
            .ToListAsync();

        if (books.Any())
        {
            var authorIds = books.Where(b => b.AuthorID > 0).Select(b => b.AuthorID).Distinct().ToList();
            var categoryIds = books.Where(b => b.CategoryID > 0).Select(b => b.CategoryID).Distinct().ToList();
            var publisherIds = books.Where(b => b.PublisherID.HasValue).Select(b => b.PublisherID!.Value).Distinct().ToList();

            var authors = authorIds.Any()
                ? await _context.Authors.Where(a => authorIds.Contains(a.AuthorID)).AsNoTracking().ToListAsync()
                : new List<Author>();
            var categoriesById = categoryIds.Any()
                ? await _context.Categories.Where(c => categoryIds.Contains(c.CategoryID)).AsNoTracking().ToListAsync()
                : new List<Category>();
            var publishers = publisherIds.Any()
                ? await _context.Publisher.Where(p => publisherIds.Contains(p.PublisherID)).AsNoTracking().ToListAsync()
                : new List<Publisher>();

            foreach (var book in books)
            {
                book.Author = authors.FirstOrDefault(a => a.AuthorID == book.AuthorID);
                book.Category = categoriesById.FirstOrDefault(c => c.CategoryID == book.CategoryID);
                book.Publisher = publishers.FirstOrDefault(p => p.PublisherID == book.PublisherID);
            }
        }

        var categories = await _context.Categories.FromSqlRaw("SELECT * FROM Categories").AsNoTracking().ToListAsync();

        var facultyId = await _context.Database
            .SqlQuery<int?>($"""
                SELECT FacultyID AS Value
                FROM Users
                WHERE UserID = {userId}
            """)
            .SingleAsync();

        var hasActiveSubscription = false;
        if (facultyId.HasValue)
        {
            var subCount = await _context.Database
                .SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value
                    FROM Subscriptions
                    WHERE FacultyID = {facultyId.Value}
                      AND (Status = 'Approved' OR Status IS NULL)
                      AND GETDATE() BETWEEN StartDate AND EndDate
                """)
                .SingleAsync();
            hasActiveSubscription = subCount > 0;
        }

        var readedBookIds = await _context.Database
            .SqlQuery<int>($"""
                SELECT DISTINCT BookID AS Value
                FROM BookLogs
                WHERE UserID = {userId}
                  AND ActionType = 'READ'
            """)
            .ToListAsync();

        var readed = await _context.Database
            .SqlQuery<int>($"""
                SELECT COUNT(DISTINCT BookID) AS Value
                FROM BookLogs
                WHERE UserID = {userId}
                  AND ActionType = 'READ'
            """)
            .SingleAsync();

        return Ok(new ClientIndexResponse
        {
            Books = books,
            Categories = categories,
            HasSubscription = hasActiveSubscription,
            SubscriptionStatus = hasActiveSubscription ? "Активна" : "Нет активной подписки",
            ReadedBookIds = readedBookIds,
            TotalBooks = books.Count,
            Readed = readed
        });
    }

    [HttpGet("book-details/{id:int}")]
    public async Task<ActionResult<ClientBookDetailsResponse>> BookDetails([FromRoute] int id)
    {
        var userId = User.GetUserId();
        var book = await _context.Books
            .FromSqlRaw("SELECT * FROM Books WHERE BookID = @id", new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (book == null)
        {
            return NotFound();
        }

        if (book.AuthorID > 0)
        {
            book.Author = await _context.Authors
                .FromSqlRaw("SELECT * FROM Authors WHERE AuthorID = @id", new SqlParameter("@id", book.AuthorID))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }
        if (book.CategoryID > 0)
        {
            book.Category = await _context.Categories
                .FromSqlRaw("SELECT * FROM Categories WHERE CategoryID = @id", new SqlParameter("@id", book.CategoryID))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }
        if (book.PublisherID.HasValue)
        {
            book.Publisher = await _context.Publisher
                .FromSqlRaw("SELECT * FROM Publisher WHERE PublisherID = @id", new SqlParameter("@id", book.PublisherID.Value))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        var facultyId = await _context.Database
            .SqlQuery<int?>($"SELECT FacultyID AS Value FROM Users WHERE UserID = {userId}")
            .SingleAsync();
        var hasActiveSubscription = false;
        if (facultyId.HasValue)
        {
            var subCount = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value
                FROM Subscriptions
                WHERE FacultyID = {facultyId.Value}
                  AND (Status = 'Approved' OR Status IS NULL)
                  AND GETDATE() BETWEEN StartDate AND EndDate
            """).SingleAsync();
            hasActiveSubscription = subCount > 0;
        }

        return Ok(new ClientBookDetailsResponse { Book = book, HasSubscription = hasActiveSubscription });
    }

    [HttpPost("mark-read")]
    public async Task<ActionResult<ApiCommandResponse>> MarkRead([FromBody] MarkAsReadRequest request)
    {
        var userId = User.GetUserId();
        await _context.Database.ExecuteSqlRawAsync("""
            INSERT INTO BookLogs (UserID, BookID, ActionType, ActionAt)
            VALUES (@userId, @bookId, 'READ', GETDATE())
        """, new SqlParameter("@userId", userId), new SqlParameter("@bookId", request.BookId));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("read-online/{id:int}")]
    public async Task<ActionResult<ClientReadOnlineResponse>> ReadOnline([FromRoute] int id)
    {
        var userId = User.GetUserId();
        var book = await _context.Books
            .FromSqlRaw("""
                SELECT BookID, Title, Description, PublishYear, CategoryID, AuthorID, PublisherID, FilePath, ImagePath, RequiresSubscription
                FROM Books WHERE BookID = @id
            """, new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (book == null)
        {
            return NotFound();
        }

        if (book.AuthorID > 0)
        {
            book.Author = await _context.Authors
                .FromSqlRaw("SELECT * FROM Authors WHERE AuthorID = @id", new SqlParameter("@id", book.AuthorID))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        var facultyId = await _context.Database.SqlQuery<int?>($"SELECT FacultyID AS Value FROM Users WHERE UserID = {userId}").SingleAsync();
        var hasActiveSubscription = false;
        if (facultyId.HasValue)
        {
            var subCount = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value
                FROM Subscriptions
                WHERE FacultyID = {facultyId.Value}
                  AND (Status = 'Approved' OR Status IS NULL)
                  AND GETDATE() BETWEEN StartDate AND EndDate
            """).SingleAsync();
            hasActiveSubscription = subCount > 0;
        }

        var canRead = !book.RequiresSubscription || hasActiveSubscription;
        if (canRead)
        {
            var hasReadLog = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value
                FROM BookLogs
                WHERE UserID = {userId}
                  AND BookID = {id}
                  AND ActionType = 'READ'
                  AND ActionAt >= DATEADD(MINUTE, -5, GETDATE())
            """).SingleAsync();

            if (hasReadLog == 0)
            {
                await _context.Database.ExecuteSqlRawAsync("""
                    INSERT INTO BookLogs (UserID, BookID, ActionType, ActionAt)
                    VALUES (@userId, @bookId, 'READ', GETDATE())
                """, new SqlParameter("@userId", userId), new SqlParameter("@bookId", id));
            }
        }

        var extension = Path.GetExtension(book.FilePath ?? "").ToLowerInvariant();
        var fileType = extension switch
        {
            ".pdf" => "pdf",
            ".epub" => "epub",
            ".fb2" => "fb2",
            ".txt" => "text",
            _ => "unknown"
        };

        return Ok(new ClientReadOnlineResponse
        {
            Book = book,
            HasSubscription = hasActiveSubscription,
            CanRead = canRead,
            FilePath = book.FilePath,
            FileType = fileType
        });
    }

    [HttpGet("file/{id:int}")]
    public async Task<IActionResult> GetFile([FromRoute] int id)
    {
        var userId = User.GetUserId();
        var book = await _context.Database.SqlQuery<BookDownloadDto>($"""
            SELECT BookID, Title, FilePath, CAST(CASE WHEN RequiresSubscription = 1 THEN 1 ELSE 0 END AS BIT) AS RequiresSubscription
            FROM Books WHERE BookID = {id}
        """).AsNoTracking().SingleOrDefaultAsync();

        if (book == null || string.IsNullOrWhiteSpace(book.FilePath))
            return NotFound();

        if (book.RequiresSubscription)
        {
            var facultyId = await _context.Database.SqlQuery<int?>($"""
                SELECT FacultyID AS Value FROM Users WHERE UserID = {userId}
            """).SingleAsync();
            var hasActiveSubscription = false;
            if (facultyId.HasValue)
            {
                var subCount = await _context.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value
                    FROM Subscriptions
                    WHERE FacultyID = {facultyId.Value}
                      AND (Status = 'Approved' OR Status IS NULL)
                      AND GETDATE() BETWEEN StartDate AND EndDate
                """).SingleAsync();
                hasActiveSubscription = subCount > 0;
            }

            if (!hasActiveSubscription)
                return Forbid();
        }

        var fullPath = ResolveBookFullPath(book.FilePath);
        if (string.IsNullOrWhiteSpace(fullPath) || !System.IO.File.Exists(fullPath))
            return NotFound();

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".epub" => "application/epub+zip",
            ".fb2" => "application/x-fictionbook+xml",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };

        var result = PhysicalFile(fullPath, contentType);
        result.EnableRangeProcessing = true;
        return result;
    }

    [HttpGet("download/{id:int}")]
    public async Task<IActionResult> Download([FromRoute] int id)
    {
        var userId = User.GetUserId();
        var isGuest = User.IsInRole("Guest");
        var book = await _context.Database.SqlQuery<BookDownloadDto>($"""
            SELECT BookID, Title, FilePath, CAST(CASE WHEN RequiresSubscription = 1 THEN 1 ELSE 0 END AS BIT) AS RequiresSubscription
            FROM Books WHERE BookID = {id}
        """).AsNoTracking().SingleOrDefaultAsync();
        if (book == null || string.IsNullOrWhiteSpace(book.FilePath))
            return NotFound();

        if (isGuest)
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Гостевой доступ не позволяет скачивать книги." });
        if (book.RequiresSubscription)
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Книги по подписке доступны только онлайн." });

        var fullPath = ResolveBookFullPath(book.FilePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        await _context.Database.ExecuteSqlRawAsync("""
            INSERT INTO BookLogs (UserID, BookID, ActionType, ActionAt)
            VALUES (@userId, @bookId, 'DOWNLOAD', GETDATE())
        """, new SqlParameter("@userId", userId), new SqlParameter("@bookId", id));

        return PhysicalFile(fullPath, "application/octet-stream", Path.GetFileName(fullPath));
    }

    [HttpGet("readed")]
    public async Task<ActionResult<ClientReadedResponse>> Readed([FromQuery] string? search)
    {
        var userId = User.GetUserId();
        var readedBookIds = await _context.Database.SqlQuery<int>($"""
            SELECT DISTINCT BookID AS Value
            FROM BookLogs
            WHERE UserID = {userId}
              AND ActionType = 'READ'
        """).ToListAsync();

        if (!readedBookIds.Any())
            return Ok(new ClientReadedResponse { Books = new List<Book>() });

        var sql = $"""
            SELECT DISTINCT
                b.BookID, b.Title, b.Description, b.PublishYear, b.CategoryID, b.AuthorID, b.PublisherID, b.FilePath, b.ImagePath, b.RequiresSubscription
            FROM Books b
            WHERE b.BookID IN ({string.Join(",", readedBookIds)})
        """;

        if (!string.IsNullOrWhiteSpace(search))
            sql += $" AND (b.Title LIKE '%{search.Replace("'", "''")}%' OR b.Description LIKE '%{search.Replace("'", "''")}%')";

        var books = await _context.Books.FromSqlRaw(sql).AsNoTracking().ToListAsync();
        if (books.Any())
        {
            var authorIds = books.Where(b => b.AuthorID > 0).Select(b => b.AuthorID).Distinct().ToList();
            var categoryIds = books.Where(b => b.CategoryID > 0).Select(b => b.CategoryID).Distinct().ToList();
            var authors = authorIds.Any()
                ? await _context.Authors.Where(a => authorIds.Contains(a.AuthorID)).AsNoTracking().ToListAsync()
                : new List<Author>();
            var categories = categoryIds.Any()
                ? await _context.Categories.Where(c => categoryIds.Contains(c.CategoryID)).AsNoTracking().ToListAsync()
                : new List<Category>();

            foreach (var book in books)
            {
                book.Author = authors.FirstOrDefault(a => a.AuthorID == book.AuthorID);
                book.Category = categories.FirstOrDefault(c => c.CategoryID == book.CategoryID);
            }
        }

        return Ok(new ClientReadedResponse { Books = books });
    }

    [HttpGet("bookmarks")]
    public async Task<ActionResult<List<Bookmark>>> GetBookmarks([FromQuery] int bookId)
    {
        var userId = User.GetUserId();
        var bookmarks = await _context.Database.SqlQuery<Bookmark>($"""
            SELECT BookmarkID, UserID, BookID, Page, Position, Title, Note, CreatedAt
            FROM Bookmarks
            WHERE UserID = {userId} AND BookID = {bookId}
            ORDER BY CreatedAt DESC
        """).ToListAsync();
        return Ok(bookmarks);
    }

    [HttpPost("bookmarks")]
    public async Task<ActionResult<ApiCommandResponse>> AddBookmark([FromBody] ClientBookmarkRequest request)
    {
        var userId = User.GetUserId();
        await _context.Database.ExecuteSqlRawAsync("""
            INSERT INTO Bookmarks (UserID, BookID, Page, Position, Title, Note, CreatedAt)
            VALUES (@userId, @bookId, @page, @position, @title, @note, GETDATE())
        """,
            new SqlParameter("@userId", userId),
            new SqlParameter("@bookId", request.Bookmark.BookID),
            new SqlParameter("@page", (object?)request.Bookmark.Page ?? DBNull.Value),
            new SqlParameter("@position", (object?)request.Bookmark.Position ?? DBNull.Value),
            new SqlParameter("@title", (object?)request.Bookmark.Title ?? DBNull.Value),
            new SqlParameter("@note", (object?)request.Bookmark.Note ?? DBNull.Value));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("bookmarks/{bookmarkId:int}")]
    public async Task<ActionResult<ApiCommandResponse>> DeleteBookmark([FromRoute] int bookmarkId)
    {
        var userId = User.GetUserId();
        var deleted = await _context.Database.ExecuteSqlRawAsync("""
            DELETE FROM Bookmarks
            WHERE BookmarkID = @bookmarkId AND UserID = @userId
        """, new SqlParameter("@bookmarkId", bookmarkId), new SqlParameter("@userId", userId));
        return Ok(new ApiCommandResponse { Success = deleted > 0 });
    }

    [HttpPut("bookmarks")]
    public async Task<ActionResult<ApiCommandResponse>> UpdateBookmark([FromBody] ClientBookmarkRequest request)
    {
        var userId = User.GetUserId();
        var updated = await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Bookmarks
            SET Page = @page, Position = @position, Title = @title, Note = @note
            WHERE BookmarkID = @bookmarkId AND UserID = @userId
        """,
            new SqlParameter("@bookmarkId", request.Bookmark.BookmarkID),
            new SqlParameter("@userId", userId),
            new SqlParameter("@page", (object?)request.Bookmark.Page ?? DBNull.Value),
            new SqlParameter("@position", (object?)request.Bookmark.Position ?? DBNull.Value),
            new SqlParameter("@title", (object?)request.Bookmark.Title ?? DBNull.Value),
            new SqlParameter("@note", (object?)request.Bookmark.Note ?? DBNull.Value));
        return Ok(new ApiCommandResponse { Success = updated > 0 });
    }

    private string? ResolveBookFullPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var trimmed = filePath.Trim();
        var root = Path.GetPathRoot(trimmed) ?? string.Empty;
        var isWindowsRoot = root.Contains(':');
        var isUnc = root.StartsWith(@"\\");

        if (isWindowsRoot || isUnc)
            return trimmed;

        var relativePath = trimmed.Replace("\\", "/").TrimStart('~').TrimStart('/', '\\');

        // 1) Current API project root (LibraryMPT.Api/wwwroot)
        var apiRootCandidate = Path.Combine(_environment.ContentRootPath, "wwwroot", relativePath);
        if (System.IO.File.Exists(apiRootCandidate))
            return apiRootCandidate;

        // 2) Legacy/main MVC project root (LibraryMPT/wwwroot) - same behavior as before API split.
        var parent = Directory.GetParent(_environment.ContentRootPath)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            var legacyRootCandidate = Path.Combine(parent, "wwwroot", relativePath);
            if (System.IO.File.Exists(legacyRootCandidate))
                return legacyRootCandidate;
        }

        // Fallback path for diagnostics/not found handling.
        return apiRootCandidate;
    }
}

