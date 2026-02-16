using LibraryMPT.Data;
using LibraryMPT.Api.Extensions;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using System.Text.RegularExpressions;

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
        var searchParam = new NpgsqlParameter("@search", NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim()
        };
        var categoryParam = new NpgsqlParameter("@categoryId", NpgsqlTypes.NpgsqlDbType.Integer)
        {
            Value = categoryId.HasValue ? categoryId.Value : DBNull.Value
        };

        var books = await _context.Books
            .FromSqlRaw("""
                SELECT
                    bookid AS "BookID",
                    title AS "Title",
                    description AS "Description",
                    publishyear AS "PublishYear",
                    categoryid AS "CategoryID",
                    authorid AS "AuthorID",
                    publisherid AS "PublisherID",
                    imagepath AS "ImagePath",
                    filepath AS "FilePath",
                    requiressubscription AS "RequiresSubscription"
                FROM books
                WHERE (@search IS NULL OR title ILIKE '%' || @search || '%' OR description ILIKE '%' || @search || '%')
                  AND (@categoryId IS NULL OR categoryid = @categoryId)
            """, searchParam, categoryParam)
            .AsNoTracking()
            .ToListAsync();

        if (books.Any())
        {
            var authorIds = books.Where(b => b.AuthorID > 0).Select(b => b.AuthorID).Distinct().ToList();
            var categoryIds = books.Where(b => b.CategoryID > 0).Select(b => b.CategoryID).Distinct().ToList();
            var publisherIds = books.Where(b => b.PublisherID.HasValue).Select(b => b.PublisherID!.Value).Distinct().ToList();

            var authors = authorIds.Any()
                ? await _context.Authors
                    .FromSqlRaw($"""
                        SELECT
                            authorid AS "AuthorID",
                            firstname AS "FirstName",
                            lastname AS "LastName"
                        FROM authors
                        WHERE authorid IN ({string.Join(",", authorIds)})
                    """)
                    .AsNoTracking()
                    .ToListAsync()
                : new List<Author>();
            var categoriesById = categoryIds.Any()
                ? await _context.Categories
                    .FromSqlRaw($"""
                        SELECT
                            categoryid AS "CategoryID",
                            categoryname AS "CategoryName"
                        FROM categories
                        WHERE categoryid IN ({string.Join(",", categoryIds)})
                    """)
                    .AsNoTracking()
                    .ToListAsync()
                : new List<Category>();
            var publishers = publisherIds.Any()
                ? await _context.Publisher
                    .FromSqlRaw($"""
                        SELECT
                            publisherid AS "PublisherID",
                            publishername AS "PublisherName"
                        FROM publisher
                        WHERE publisherid IN ({string.Join(",", publisherIds)})
                    """)
                    .AsNoTracking()
                    .ToListAsync()
                : new List<Publisher>();

            foreach (var book in books)
            {
                book.Author = authors.FirstOrDefault(a => a.AuthorID == book.AuthorID);
                book.Category = categoriesById.FirstOrDefault(c => c.CategoryID == book.CategoryID);
                book.Publisher = publishers.FirstOrDefault(p => p.PublisherID == book.PublisherID);
            }
        }

        var categories = await _context.Categories.FromSqlRaw("""
            SELECT
                categoryid AS "CategoryID",
                categoryname AS "CategoryName"
            FROM categories
        """).AsNoTracking().ToListAsync();

        var facultyId = await _context.Database
            .SqlQuery<int?>($"""
                SELECT facultyid AS "Value"
                FROM users
                WHERE userid = {userId}
            """)
            .SingleOrDefaultAsync();

        var hasActiveSubscription = false;
        if (facultyId.HasValue)
        {
            var subCount = await _context.Database
                .SqlQuery<int>($"""
                    SELECT COUNT(*) AS "Value"
                    FROM Subscriptions
                    WHERE FacultyID = {facultyId.Value}
                      AND (Status = 'Approved' OR Status IS NULL)
                      AND NOW() BETWEEN StartDate AND EndDate
                """)
                .SingleAsync();
            hasActiveSubscription = subCount > 0;
        }

        var readedBookIds = await _context.Database
            .SqlQuery<int>($"""
                SELECT DISTINCT bookid AS "Value"
                FROM booklogs
                WHERE userid = {userId}
                  AND actiontype = 'READ'
            """)
            .ToListAsync();

        var readed = await _context.Database
            .SqlQuery<int>($"""
                SELECT COUNT(DISTINCT BookID) AS "Value"
                FROM BookLogs
                WHERE UserID = {userId}
                  AND ActionType = 'READ'
            """)
            .SingleOrDefaultAsync();

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
            .FromSqlRaw("""
                SELECT
                    bookid AS "BookID",
                    title AS "Title",
                    description AS "Description",
                    publishyear AS "PublishYear",
                    categoryid AS "CategoryID",
                    authorid AS "AuthorID",
                    publisherid AS "PublisherID",
                    imagepath AS "ImagePath",
                    filepath AS "FilePath",
                    requiressubscription AS "RequiresSubscription"
                FROM books
                WHERE bookid = @id
            """, new NpgsqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (book == null)
        {
            return NotFound();
        }

        if (book.AuthorID > 0)
        {
            book.Author = await _context.Authors
                .FromSqlRaw("""
                    SELECT
                        authorid AS "AuthorID",
                        firstname AS "FirstName",
                        lastname AS "LastName"
                    FROM authors
                    WHERE authorid = @id
                """, new NpgsqlParameter("@id", book.AuthorID))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }
        if (book.CategoryID > 0)
        {
            book.Category = await _context.Categories
                .FromSqlRaw("""
                    SELECT
                        categoryid AS "CategoryID",
                        categoryname AS "CategoryName"
                    FROM categories
                    WHERE categoryid = @id
                """, new NpgsqlParameter("@id", book.CategoryID))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }
        if (book.PublisherID.HasValue)
        {
            book.Publisher = await _context.Publisher
                .FromSqlRaw("""
                    SELECT
                        publisherid AS "PublisherID",
                        publishername AS "PublisherName"
                    FROM publisher
                    WHERE publisherid = @id
                """, new NpgsqlParameter("@id", book.PublisherID.Value))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        var facultyId = await _context.Database
            .SqlQuery<int?>($"SELECT FacultyID AS \"Value\" FROM Users WHERE UserID = {userId}")
            .SingleOrDefaultAsync();
        var hasActiveSubscription = false;
        if (facultyId.HasValue)
        {
            var subCount = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS "Value"
                FROM Subscriptions
                WHERE FacultyID = {facultyId.Value}
                  AND (Status = 'Approved' OR Status IS NULL)
                  AND NOW() BETWEEN StartDate AND EndDate
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
            VALUES (@userId, @bookId, 'READ', NOW())
        """, new NpgsqlParameter("@userId", userId), new NpgsqlParameter("@bookId", request.BookId));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("read-online/{id:int}")]
    public async Task<ActionResult<ClientReadOnlineResponse>> ReadOnline([FromRoute] int id)
    {
        var userId = User.GetUserId();
        var book = await _context.Books
            .FromSqlRaw("""
                SELECT
                    bookid AS "BookID",
                    title AS "Title",
                    description AS "Description",
                    publishyear AS "PublishYear",
                    categoryid AS "CategoryID",
                    authorid AS "AuthorID",
                    publisherid AS "PublisherID",
                    filepath AS "FilePath",
                    imagepath AS "ImagePath",
                    requiressubscription AS "RequiresSubscription"
                FROM books
                WHERE bookid = @id
            """, new NpgsqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (book == null)
        {
            return NotFound();
        }

        if (book.AuthorID > 0)
        {
            book.Author = await _context.Authors
                .FromSqlRaw("""
                    SELECT
                        authorid AS "AuthorID",
                        firstname AS "FirstName",
                        lastname AS "LastName"
                    FROM authors
                    WHERE authorid = @id
                """, new NpgsqlParameter("@id", book.AuthorID))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        var facultyId = await _context.Database
            .SqlQuery<int?>($"SELECT FacultyID AS \"Value\" FROM Users WHERE UserID = {userId}")
            .SingleOrDefaultAsync();
        var hasActiveSubscription = false;
        if (facultyId.HasValue)
        {
            var subCount = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS "Value"
                FROM Subscriptions
                WHERE FacultyID = {facultyId.Value}
                  AND (Status = 'Approved' OR Status IS NULL)
                  AND NOW() BETWEEN StartDate AND EndDate
            """).SingleAsync();
            hasActiveSubscription = subCount > 0;
        }

        var canRead = !book.RequiresSubscription || hasActiveSubscription;
        if (canRead)
        {
            var hasReadLog = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS "Value"
                FROM BookLogs
                WHERE UserID = {userId}
                  AND BookID = {id}
                  AND ActionType = 'READ'
                  AND ActionAt >= NOW() - INTERVAL '5 minutes'
            """).SingleAsync();

            if (hasReadLog == 0)
            {
                await _context.Database.ExecuteSqlRawAsync("""
                    INSERT INTO BookLogs (UserID, BookID, ActionType, ActionAt)
                    VALUES (@userId, @bookId, 'READ', NOW())
                """, new NpgsqlParameter("@userId", userId), new NpgsqlParameter("@bookId", id));
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
            SELECT
                bookid AS "BookID",
                title AS "Title",
                filepath AS "FilePath",
                requiressubscription AS "RequiresSubscription"
            FROM books
            WHERE bookid = {id}
        """).AsNoTracking().SingleOrDefaultAsync();

        if (book == null || string.IsNullOrWhiteSpace(book.FilePath))
            return NotFound();

        if (book.RequiresSubscription)
        {
            var facultyId = await _context.Database.SqlQuery<int?>($"""
                SELECT FacultyID AS "Value" FROM Users WHERE UserID = {userId}
            """).SingleOrDefaultAsync();
            var hasActiveSubscription = false;
            if (facultyId.HasValue)
            {
                var subCount = await _context.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS "Value"
                    FROM Subscriptions
                    WHERE FacultyID = {facultyId.Value}
                      AND (Status = 'Approved' OR Status IS NULL)
                      AND NOW() BETWEEN StartDate AND EndDate
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
            SELECT
                bookid AS "BookID",
                title AS "Title",
                filepath AS "FilePath",
                requiressubscription AS "RequiresSubscription"
            FROM books
            WHERE bookid = {id}
        """).AsNoTracking().SingleOrDefaultAsync();
        if (book == null || string.IsNullOrWhiteSpace(book.FilePath))
            return NotFound();

        if (isGuest)
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Гостевой доступ не позволяет скачивать книги." });
        if (book.RequiresSubscription)
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Книга доступна только по подписке вашего учебного заведения." });

        var fullPath = ResolveBookFullPath(book.FilePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        await _context.Database.ExecuteSqlRawAsync("""
            INSERT INTO BookLogs (UserID, BookID, ActionType, ActionAt)
            VALUES (@userId, @bookId, 'DOWNLOAD', NOW())
        """, new NpgsqlParameter("@userId", userId), new NpgsqlParameter("@bookId", id));

        return PhysicalFile(fullPath, "application/octet-stream", Path.GetFileName(fullPath));
    }

    [HttpGet("readed")]
    public async Task<ActionResult<ClientReadedResponse>> Readed([FromQuery] string? search)
    {
        var userId = User.GetUserId();
        var readedBookIds = await _context.Database.SqlQuery<int>($"""
            SELECT DISTINCT BookID AS "Value"
            FROM BookLogs
            WHERE UserID = {userId}
              AND ActionType = 'READ'
        """).ToListAsync();

        if (!readedBookIds.Any())
            return Ok(new ClientReadedResponse { Books = new List<Book>() });

        var sql = $"""
            SELECT DISTINCT
                b.bookid AS "BookID",
                b.title AS "Title",
                b.description AS "Description",
                b.publishyear AS "PublishYear",
                b.categoryid AS "CategoryID",
                b.authorid AS "AuthorID",
                b.publisherid AS "PublisherID",
                b.filepath AS "FilePath",
                b.imagepath AS "ImagePath",
                b.requiressubscription AS "RequiresSubscription"
            FROM books b
            WHERE b.bookid IN ({string.Join(",", readedBookIds)})
        """;

        if (!string.IsNullOrWhiteSpace(search))
            sql += $" AND (b.Title ILIKE '%{search.Replace("'", "''")}%' OR b.Description ILIKE '%{search.Replace("'", "''")}%')";

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
            SELECT
                bookmarkid AS "BookmarkID",
                userid AS "UserID",
                bookid AS "BookID",
                COALESCE(page::text, '') AS "Page",
                position AS "Position",
                title AS "Title",
                note AS "Note",
                createdat AS "CreatedAt"
            FROM bookmarks
            WHERE userid = {userId} AND bookid = {bookId}
            ORDER BY createdat DESC
        """).ToListAsync();
        return Ok(bookmarks);
    }

    [HttpPost("bookmarks")]
    public async Task<ActionResult<ApiCommandResponse>> AddBookmark([FromBody] ClientBookmarkRequest request)
    {
        var userId = User.GetUserId();
        var parsedPage = ParsePageNumber(request.Bookmark.Page);
        await _context.Database.ExecuteSqlRawAsync("""
            INSERT INTO bookmarks (userid, bookid, page, position, title, note, createdat)
            VALUES (@userId, @bookId, @page, @position, @title, @note, NOW())
        """,
            new NpgsqlParameter("@userId", userId),
            new NpgsqlParameter("@bookId", request.Bookmark.BookID),
            new NpgsqlParameter("@page", NpgsqlDbType.Integer) { Value = parsedPage.HasValue ? parsedPage.Value : DBNull.Value },
            new NpgsqlParameter("@position", (object?)request.Bookmark.Position ?? DBNull.Value),
            new NpgsqlParameter("@title", (object?)request.Bookmark.Title ?? DBNull.Value),
            new NpgsqlParameter("@note", (object?)request.Bookmark.Note ?? DBNull.Value));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("bookmarks/{bookmarkId:int}")]
    public async Task<ActionResult<ApiCommandResponse>> DeleteBookmark([FromRoute] int bookmarkId)
    {
        var userId = User.GetUserId();
        var deleted = await _context.Database.ExecuteSqlRawAsync("""
            DELETE FROM Bookmarks
            WHERE BookmarkID = @bookmarkId AND UserID = @userId
        """, new NpgsqlParameter("@bookmarkId", bookmarkId), new NpgsqlParameter("@userId", userId));
        return Ok(new ApiCommandResponse { Success = deleted > 0 });
    }

    [HttpPut("bookmarks")]
    public async Task<ActionResult<ApiCommandResponse>> UpdateBookmark([FromBody] ClientBookmarkRequest request)
    {
        var userId = User.GetUserId();
        var parsedPage = ParsePageNumber(request.Bookmark.Page);
        var updated = await _context.Database.ExecuteSqlRawAsync("""
            UPDATE bookmarks
            SET page = @page, position = @position, title = @title, note = @note
            WHERE bookmarkid = @bookmarkId AND userid = @userId
        """,
            new NpgsqlParameter("@bookmarkId", request.Bookmark.BookmarkID),
            new NpgsqlParameter("@userId", userId),
            new NpgsqlParameter("@page", NpgsqlDbType.Integer) { Value = parsedPage.HasValue ? parsedPage.Value : DBNull.Value },
            new NpgsqlParameter("@position", (object?)request.Bookmark.Position ?? DBNull.Value),
            new NpgsqlParameter("@title", (object?)request.Bookmark.Title ?? DBNull.Value),
            new NpgsqlParameter("@note", (object?)request.Bookmark.Note ?? DBNull.Value));
        return Ok(new ApiCommandResponse { Success = updated > 0 });
    }

    private static int? ParsePageNumber(string? rawPage)
    {
        if (string.IsNullOrWhiteSpace(rawPage))
        {
            return null;
        }

        if (int.TryParse(rawPage, out var direct))
        {
            return direct;
        }

        var match = Regex.Match(rawPage, @"\d+");
        return match.Success && int.TryParse(match.Value, out var parsed) ? parsed : null;
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

