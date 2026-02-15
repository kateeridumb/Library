using LibraryMPT.Data;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LibraryMPT.Api.Controllers;

[ApiController]
[Route("api/books")]
public sealed class BooksApiController : ControllerBase
{
    private readonly LibraryContext _context;

    public BooksApiController(LibraryContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Book>>> GetAll()
    {
        var books = await _context.Books
            .FromSqlRaw("""
                SELECT
                    BookID,
                    Title,
                    Description,
                    PublishYear,
                    CategoryID,
                    AuthorID,
                    PublisherID,
                    FilePath,
                    ImagePath,
                    RequiresSubscription
                FROM Books
                ORDER BY BookID
            """)
            .AsNoTracking()
            .ToListAsync();

        return Ok(books);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Book>> GetById(int id)
    {
        var book = await _context.Books
            .FromSqlRaw("""
                SELECT
                    BookID,
                    Title,
                    Description,
                    PublishYear,
                    CategoryID,
                    AuthorID,
                    PublisherID,
                    FilePath,
                    ImagePath,
                    RequiresSubscription
                FROM Books
                WHERE BookID = @id
            """, new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (book == null)
        {
            return NotFound();
        }

        return Ok(book);
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups()
    {
        var authors = await _context.Authors
            .FromSqlRaw("SELECT * FROM Authors")
            .AsNoTracking()
            .ToListAsync();
        var categories = await _context.Categories
            .FromSqlRaw("SELECT * FROM Categories")
            .AsNoTracking()
            .ToListAsync();
        var publishers = await _context.Publisher
            .FromSqlRaw("SELECT * FROM Publisher")
            .AsNoTracking()
            .ToListAsync();

        return Ok(new
        {
            authors,
            categories,
            publishers
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Book book)
    {
        await _context.Database.ExecuteSqlRawAsync("""
            INSERT INTO Books
            (Title, Description, PublishYear, CategoryID, AuthorID, PublisherID, FilePath, ImagePath)
            VALUES
            (@Title, @Description, @PublishYear, @CategoryID, @AuthorID, @PublisherID, @FilePath, @ImagePath)
        """,
            new SqlParameter("@Title", book.Title),
            new SqlParameter("@Description", (object?)book.Description ?? DBNull.Value),
            new SqlParameter("@PublishYear", (object?)book.PublishYear ?? DBNull.Value),
            new SqlParameter("@CategoryID", book.CategoryID),
            new SqlParameter("@AuthorID", book.AuthorID),
            new SqlParameter("@PublisherID", (object?)book.PublisherID ?? DBNull.Value),
            new SqlParameter("@FilePath", (object?)book.FilePath ?? DBNull.Value),
            new SqlParameter("@ImagePath", (object?)book.ImagePath ?? DBNull.Value)
        );

        return Ok();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Book book)
    {
        if (id != book.BookID)
        {
            return BadRequest();
        }

        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Books SET
                Title = @Title,
                Description = @Description,
                PublishYear = @PublishYear,
                CategoryID = @CategoryID,
                AuthorID = @AuthorID,
                PublisherID = @PublisherID,
                FilePath = @FilePath,
                ImagePath = @ImagePath
            WHERE BookID = @BookID
        """,
            new SqlParameter("@Title", book.Title),
            new SqlParameter("@Description", (object?)book.Description ?? DBNull.Value),
            new SqlParameter("@PublishYear", (object?)book.PublishYear ?? DBNull.Value),
            new SqlParameter("@CategoryID", book.CategoryID),
            new SqlParameter("@AuthorID", book.AuthorID),
            new SqlParameter("@PublisherID", (object?)book.PublisherID ?? DBNull.Value),
            new SqlParameter("@FilePath", (object?)book.FilePath ?? DBNull.Value),
            new SqlParameter("@ImagePath", (object?)book.ImagePath ?? DBNull.Value),
            new SqlParameter("@BookID", book.BookID)
        );

        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM Books WHERE BookID = @id",
            new SqlParameter("@id", id)
        );

        return Ok();
    }
}

