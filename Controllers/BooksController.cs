using LibraryMPT.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace LibraryMPT.Controllers
{
    public class BooksController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BooksController> _logger;

        public BooksController(IHttpClientFactory httpClientFactory, ILogger<BooksController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var books = await api.GetFromJsonAsync<List<Book>>("api/books") ?? new List<Book>();

            return View(books);
        }

        public async Task<IActionResult> Details(int id)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var book = await api.GetFromJsonAsync<Book>($"api/books/{id}");

            if (book == null)
                return NotFound();

            return View(book);
        }

        public async Task<IActionResult> Create()
        {
            await LoadSelectLists();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Book book)
        {
            if (!ModelState.IsValid)
            {
                await LoadSelectLists();
                return View(book);
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.PostAsJsonAsync("api/books", book);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Books API create failed with status {StatusCode}", response.StatusCode);
                ModelState.AddModelError(string.Empty, "Не удалось создать книгу через API.");
                await LoadSelectLists();
                return View(book);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var book = await api.GetFromJsonAsync<Book>($"api/books/{id}");

            if (book == null)
                return NotFound();

            await LoadSelectLists();
            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Book book)
        {
            if (id != book.BookID)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await LoadSelectLists();
                return View(book);
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.PutAsJsonAsync($"api/books/{id}", book);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Books API update failed with status {StatusCode}", response.StatusCode);
                ModelState.AddModelError(string.Empty, "Не удалось обновить книгу через API.");
                await LoadSelectLists();
                return View(book);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var book = await api.GetFromJsonAsync<Book>($"api/books/{id}");

            if (book == null)
                return NotFound();

            return View(book);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.DeleteAsync($"api/books/{id}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Books API delete failed with status {StatusCode}", response.StatusCode);
                TempData["Error"] = "Не удалось удалить книгу через API.";
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadSelectLists()
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var lookups = await api.GetFromJsonAsync<BookLookupsResponse>("api/books/lookups");
            ViewBag.Authors = lookups?.Authors ?? new List<Author>();
            ViewBag.Categories = lookups?.Categories ?? new List<Category>();
            ViewBag.Publishers = lookups?.Publishers ?? new List<Publisher>();
        }

        private sealed class BookLookupsResponse
        {
            public List<Author> Authors { get; set; } = new();
            public List<Category> Categories { get; set; } = new();
            public List<Publisher> Publishers { get; set; } = new();
        }
    }
}
 