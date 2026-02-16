using LibraryMPT.Data;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

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
            .FromSqlRaw("""
                SELECT
                    (SELECT COUNT(*) FROM books) AS "BooksCount",
                    (SELECT COUNT(*) FROM categories) AS "CategoriesCount",
                    (SELECT COUNT(*) FROM users WHERE roleid = 3) AS "ActiveReadersCount",
                    (SELECT COUNT(*) FROM booklogs WHERE actionat >= date_trunc('month', NOW())) AS "ActionsThisMonth"
            """)
            .AsEnumerable()
            .FirstOrDefault() ?? new LibrarianStatsDto();

        var categoryStats = await _context.CategoryStats
            .FromSqlRaw("""
                SELECT
                    c.categoryname AS "CategoryName",
                    COUNT(b.bookid)::int AS "BooksCount"
                FROM categories c
                LEFT JOIN books b ON b.categoryid = c.categoryid
                GROUP BY c.categoryname
                ORDER BY COUNT(b.bookid) DESC
            """)
            .AsNoTracking()
            .ToListAsync();

        var lastBooks = await _context.LastBooks
            .FromSqlRaw("""
                SELECT
                    b.title AS "Title",
                    CONCAT(a.firstname, ' ', a.lastname) AS "Author",
                    c.categoryname AS "Category"
                FROM books b
                INNER JOIN authors a ON a.authorid = b.authorid
                INNER JOIN categories c ON c.categoryid = b.categoryid
                ORDER BY b.bookid DESC
                LIMIT 3
            """)
            .AsNoTracking()
            .ToListAsync();

        var pendingSubscriptionsCount = await _context.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM subscriptions WHERE status = 'Pending'")
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
        var searchParam = new NpgsqlParameter("@search", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim()
        };
        var categoryParam = new NpgsqlParameter("@categoryId", NpgsqlDbType.Integer)
        {
            Value = categoryId.HasValue ? categoryId.Value : DBNull.Value
        };
        var authorParam = new NpgsqlParameter("@authorId", NpgsqlDbType.Integer)
        {
            Value = authorId.HasValue ? authorId.Value : DBNull.Value
        };
        var requiresSubscriptionParam = new NpgsqlParameter("@requiresSubscription", NpgsqlDbType.Boolean)
        {
            Value = requiresSubscription.HasValue ? requiresSubscription.Value : DBNull.Value
        };

        var books = await _context.Books
            .FromSqlRaw("""
                SELECT
                    b.bookid AS "BookID",
                    b.title AS "Title",
                    b.authorid AS "AuthorID",
                    b.imagepath AS "ImagePath",
                    b.description AS "Description",
                    b.categoryid AS "CategoryID",
                    b.filepath AS "FilePath",
                    b.publishyear AS "PublishYear",
                    b.publisherid AS "PublisherID",
                    b.requiressubscription AS "RequiresSubscription"
                FROM books b
                WHERE
                    (@search IS NULL OR b.title ILIKE '%' || @search || '%')
                    AND (@categoryId IS NULL OR b.categoryid = @categoryId)
                    AND (@authorId IS NULL OR b.authorid = @authorId)
                    AND (@requiresSubscription IS NULL OR b.requiressubscription = @requiresSubscription)
                ORDER BY b.title
            """, searchParam, categoryParam, authorParam, requiresSubscriptionParam)
            .AsNoTracking()
            .ToListAsync();

        var categories = await _context.Categories
            .FromSqlRaw("""
                SELECT
                    categoryid AS "CategoryID",
                    categoryname AS "CategoryName"
                FROM categories
                ORDER BY categoryname
            """)
            .AsNoTracking()
            .ToListAsync();

        var authors = await _context.Authors
            .FromSqlRaw("""
                SELECT
                    authorid AS "AuthorID",
                    firstname AS "FirstName",
                    lastname AS "LastName"
                FROM authors
                ORDER BY lastname, firstname
            """)
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

        return book is null ? NotFound() : Ok(book);
    }

    [HttpGet("book-lookups")]
    public async Task<ActionResult<LibrarianBookFormLookupsResponse>> BookLookups()
    {
        var categories = await _context.Categories
            .FromSqlRaw("""
                SELECT
                    categoryid AS "CategoryID",
                    categoryname AS "CategoryName"
                FROM categories
            """)
            .AsNoTracking()
            .ToListAsync();

        var authors = await _context.Authors
            .FromSqlRaw("""
                SELECT
                    authorid AS "AuthorID",
                    firstname AS "FirstName",
                    lastname AS "LastName"
                FROM authors
            """)
            .AsNoTracking()
            .ToListAsync();

        var publishers = await _context.Publisher
            .FromSqlRaw("""
                SELECT
                    publisherid AS "PublisherID",
                    publishername AS "PublisherName"
                FROM publisher
            """)
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
        await _context.Database.ExecuteSqlRawAsync("""
            INSERT INTO books (title, description, publishyear, categoryid, authorid, publisherid, filepath, requiressubscription)
            VALUES (@title, @description, @year, @categoryId, @authorId, @publisherId, @filePath, @requiresSubscription)
        """,
            new NpgsqlParameter("@title", book.Title.Trim()),
            new NpgsqlParameter("@description", (object?)book.Description?.Trim() ?? DBNull.Value),
            new NpgsqlParameter("@year", (object?)book.PublishYear ?? DBNull.Value),
            new NpgsqlParameter("@categoryId", book.CategoryID),
            new NpgsqlParameter("@authorId", book.AuthorID),
            new NpgsqlParameter("@publisherId", (object?)book.PublisherID ?? DBNull.Value),
            new NpgsqlParameter("@filePath", (object?)book.FilePath?.Trim() ?? DBNull.Value),
            new NpgsqlParameter("@requiresSubscription", book.RequiresSubscription));

        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("book/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> EditBook(int id, [FromBody] Book book)
    {
        if (id != book.BookID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Book id mismatch." });
        }

        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE books
            SET title = @title,
                description = @description,
                publishyear = @year,
                categoryid = @categoryId,
                authorid = @authorId,
                publisherid = @publisherId,
                filepath = @filePath,
                requiressubscription = @requiresSubscription
            WHERE bookid = @id
        """,
            new NpgsqlParameter("@title", book.Title.Trim()),
            new NpgsqlParameter("@description", (object?)book.Description?.Trim() ?? DBNull.Value),
            new NpgsqlParameter("@year", (object?)book.PublishYear ?? DBNull.Value),
            new NpgsqlParameter("@categoryId", book.CategoryID),
            new NpgsqlParameter("@authorId", book.AuthorID),
            new NpgsqlParameter("@publisherId", (object?)book.PublisherID ?? DBNull.Value),
            new NpgsqlParameter("@filePath", (object?)book.FilePath?.Trim() ?? DBNull.Value),
            new NpgsqlParameter("@requiresSubscription", book.RequiresSubscription),
            new NpgsqlParameter("@id", book.BookID));

        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("book/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> DeleteBook(int id)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM books WHERE bookid = @id",
            new NpgsqlParameter("@id", id));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<Category>>> Categories([FromQuery] string? search)
    {
        var categories = await _context.Categories
            .FromSqlRaw("""
                SELECT
                    categoryid AS "CategoryID",
                    categoryname AS "CategoryName"
                FROM categories
                WHERE (@search IS NULL OR categoryname ILIKE '%' || @search || '%')
                ORDER BY categoryname
            """, new NpgsqlParameter("@search", NpgsqlTypes.NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim() })
            .AsNoTracking()
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("categories/{id:int}")]
    public async Task<ActionResult<Category>> CategoryById(int id)
    {
        var category = await _context.Categories
            .FromSqlRaw("""
                SELECT
                    categoryid AS "CategoryID",
                    categoryname AS "CategoryName"
                FROM categories
                WHERE categoryid = @id
            """, new NpgsqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return category is null ? NotFound() : Ok(category);
    }

    [HttpPost("categories")]
    public async Task<ActionResult<ApiCommandResponse>> AddCategory([FromBody] Category category)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO categories (categoryname) VALUES (@name)",
            new NpgsqlParameter("@name", category.CategoryName.Trim()));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("categories/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> EditCategory(int id, [FromBody] Category category)
    {
        if (id != category.CategoryID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Category id mismatch." });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE categories SET categoryname = @name WHERE categoryid = @id",
            new NpgsqlParameter("@name", category.CategoryName.Trim()),
            new NpgsqlParameter("@id", category.CategoryID));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> DeleteCategory(int id)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM categories WHERE categoryid = @id",
            new NpgsqlParameter("@id", id));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("authors")]
    public async Task<ActionResult<List<Author>>> Authors([FromQuery] string? search)
    {
        var authors = await _context.Authors
            .FromSqlRaw("""
                SELECT
                    authorid AS "AuthorID",
                    firstname AS "FirstName",
                    lastname AS "LastName"
                FROM authors
                WHERE (@search IS NULL OR firstname ILIKE '%' || @search || '%' OR lastname ILIKE '%' || @search || '%')
                ORDER BY lastname, firstname
            """, new NpgsqlParameter("@search", NpgsqlTypes.NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim() })
            .AsNoTracking()
            .ToListAsync();

        return Ok(authors);
    }

    [HttpGet("authors/{id:int}")]
    public async Task<ActionResult<Author>> AuthorById(int id)
    {
        var author = await _context.Authors
            .FromSqlRaw("""
                SELECT
                    authorid AS "AuthorID",
                    firstname AS "FirstName",
                    lastname AS "LastName"
                FROM authors
                WHERE authorid = @id
            """, new NpgsqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return author is null ? NotFound() : Ok(author);
    }

    [HttpPost("authors")]
    public async Task<ActionResult<ApiCommandResponse>> AddAuthor([FromBody] Author author)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO authors (firstname, lastname) VALUES (@firstName, @lastName)",
            new NpgsqlParameter("@firstName", author.FirstName.Trim()),
            new NpgsqlParameter("@lastName", author.LastName.Trim()));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("authors/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> EditAuthor(int id, [FromBody] Author author)
    {
        if (id != author.AuthorID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Author id mismatch." });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE authors SET firstname = @firstName, lastname = @lastName WHERE authorid = @id",
            new NpgsqlParameter("@firstName", author.FirstName.Trim()),
            new NpgsqlParameter("@lastName", author.LastName.Trim()),
            new NpgsqlParameter("@id", author.AuthorID));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("authors/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> DeleteAuthor(int id)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM authors WHERE authorid = @id",
            new NpgsqlParameter("@id", id));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("publishers")]
    public async Task<ActionResult<List<Publisher>>> Publishers([FromQuery] string? search)
    {
        var publishers = await _context.Publisher
            .FromSqlRaw("""
                SELECT
                    publisherid AS "PublisherID",
                    publishername AS "PublisherName"
                FROM publisher
                WHERE (@search IS NULL OR publishername ILIKE '%' || @search || '%')
                ORDER BY publishername
            """, new NpgsqlParameter("@search", NpgsqlTypes.NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim() })
            .AsNoTracking()
            .ToListAsync();

        return Ok(publishers);
    }

    [HttpGet("publishers/{id:int}")]
    public async Task<ActionResult<Publisher>> PublisherById(int id)
    {
        var publisher = await _context.Publisher
            .FromSqlRaw("""
                SELECT
                    publisherid AS "PublisherID",
                    publishername AS "PublisherName"
                FROM publisher
                WHERE publisherid = @id
            """, new NpgsqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return publisher is null ? NotFound() : Ok(publisher);
    }

    [HttpPost("publishers")]
    public async Task<ActionResult<ApiCommandResponse>> AddPublisher([FromBody] Publisher publisher)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO publisher (publishername) VALUES (@name)",
            new NpgsqlParameter("@name", publisher.PublisherName.Trim()));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("publishers/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> EditPublisher(int id, [FromBody] Publisher publisher)
    {
        if (id != publisher.PublisherID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Publisher id mismatch." });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE publisher SET publishername = @name WHERE publisherid = @id",
            new NpgsqlParameter("@name", publisher.PublisherName.Trim()),
            new NpgsqlParameter("@id", publisher.PublisherID));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("publishers/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> DeletePublisher(int id)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM publisher WHERE publisherid = @id",
            new NpgsqlParameter("@id", id));
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
                    s.subscriptionid AS "SubscriptionID",
                    s.facultyid AS "FacultyID",
                    s.name AS "SubscriptionName",
                    s.durationdays AS "DurationDays",
                    s.status AS "Status",
                    s.requestedbyuserid AS "RequestedByUserID",
                    u.username AS "RequestedByUsername",
                    u.email AS "RequestedByEmail",
                    f.facultyname AS "FacultyName",
                    (
                        SELECT COUNT(*)::int
                        FROM users u2
                        JOIN roles r ON r.roleid = u2.roleid
                        WHERE u2.facultyid = s.facultyid AND r.rolename = 'Student'
                    ) AS "StudentsCount"
                FROM subscriptions s
                LEFT JOIN users u ON u.userid = s.requestedbyuserid
                LEFT JOIN faculty f ON f.facultyid = s.facultyid
                WHERE s.requestedbyuserid IS NOT NULL
                  AND (@status IS NULL OR s.status = @status)
                ORDER BY
                    CASE WHEN s.status = 'Pending' THEN 1
                         WHEN s.status = 'Rejected' THEN 2
                         WHEN s.status = 'Approved' THEN 3
                         ELSE 4 END,
                    s.subscriptionid DESC
            """, new NpgsqlParameter("@status", NpgsqlTypes.NpgsqlDbType.Text) { Value = statusParam })
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
                SELECT *
                FROM subscriptions
                WHERE subscriptionid = @id AND status = 'Pending'
            """, new NpgsqlParameter("@id", subscriptionId))
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
            UPDATE subscriptions
            SET status = 'Approved',
                startdate = @startDate,
                enddate = @endDate
            WHERE subscriptionid = @id
        """,
            new NpgsqlParameter("@startDate", startDate),
            new NpgsqlParameter("@endDate", endDate),
            new NpgsqlParameter("@id", subscriptionId));

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
                SELECT *
                FROM subscriptions
                WHERE subscriptionid = @id AND status = 'Pending'
            """, new NpgsqlParameter("@id", subscriptionId))
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
            UPDATE subscriptions
            SET status = 'Rejected'
            WHERE subscriptionid = @id
        """, new NpgsqlParameter("@id", subscriptionId));

        return Ok(new ApiCommandResponse
        {
            Success = true,
            Message = $"Заявка на подписку \"{subscription.Name}\" отклонена."
        });
    }
}

