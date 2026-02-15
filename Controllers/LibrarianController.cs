using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net.Http.Json;

[Authorize(Roles = "Librarian")]
public class LibrarianController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LibrarianController> _logger;

    public LibrarianController(IHttpClientFactory httpClientFactory, ILogger<LibrarianController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var data = await api.GetFromJsonAsync<LibrarianDashboardResponse>("api/librarian/dashboard")
            ?? new LibrarianDashboardResponse();
        ViewBag.Stats = data.Stats;
        ViewBag.CategoryStats = data.CategoryStats;
        ViewBag.LastBooks = data.LastBooks;
        ViewBag.PendingSubscriptionsCount = data.PendingSubscriptionsCount;

        return View();
    }


    public async Task<IActionResult> BookManagement(string search, int? categoryId, int? authorId, bool? requiresSubscription)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var query = $"api/librarian/books?search={Uri.EscapeDataString(search ?? string.Empty)}" +
                    $"&categoryId={(categoryId.HasValue ? categoryId.Value.ToString() : string.Empty)}" +
                    $"&authorId={(authorId.HasValue ? authorId.Value.ToString() : string.Empty)}" +
                    $"&requiresSubscription={(requiresSubscription.HasValue ? requiresSubscription.Value.ToString().ToLowerInvariant() : string.Empty)}";
        var data = await api.GetFromJsonAsync<LibrarianBookManagementResponse>(query)
            ?? new LibrarianBookManagementResponse();

        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.AuthorId = authorId;
        ViewBag.RequiresSubscription = requiresSubscription;
        ViewBag.Categories = data.Categories;
        ViewBag.Authors = data.Authors;

        return View(data.Books);
    }


    public IActionResult AddBook()
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var lookups = api.GetFromJsonAsync<LibrarianBookFormLookupsResponse>("api/librarian/book-lookups")
            .GetAwaiter().GetResult() ?? new LibrarianBookFormLookupsResponse();
        ViewBag.Categories = lookups.Categories;
        ViewBag.Authors = lookups.Authors;
        ViewBag.Publishers = lookups.Publishers;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> AddBook(Book book)
    {
        if (string.IsNullOrWhiteSpace(book.Title))
        {
            ModelState.AddModelError("", "�������� ����� �����������");
        }
        else if (book.Title.Length > 200)
        {
            ModelState.AddModelError("", "�������� ����� �� ������ ��������� 200 ��������");
        }

        if (book.CategoryID <= 0)
        {
            ModelState.AddModelError("", "���������� ������� ���������");
        }

        if (book.AuthorID <= 0)
        {
            ModelState.AddModelError("", "���������� ������� ������");
        }

        if (string.IsNullOrWhiteSpace(book.FilePath))
        {
            ModelState.AddModelError("", "���� � ����� ����������");
        }
        else if (book.FilePath.Length > 500)
        {
            ModelState.AddModelError("", "���� � ����� �� ������ ��������� 500 ��������");
        }

        if (book.PublishYear.HasValue && (book.PublishYear < 1000 || book.PublishYear > DateTime.Now.Year + 1))
        {
            ModelState.AddModelError("", $"��� ������� ������ ���� ����� 1000 � {DateTime.Now.Year + 1}");
        }

        if (book.Description != null && book.Description.Length > 2000)
        {
            ModelState.AddModelError("", "�������� �� ������ ��������� 2000 ��������");
        }

        if (!ModelState.IsValid)
        {
            var apiValidation = _httpClientFactory.CreateClient("LibraryApi");
            var lookups = await apiValidation.GetFromJsonAsync<LibrarianBookFormLookupsResponse>("api/librarian/book-lookups")
                ?? new LibrarianBookFormLookupsResponse();
            ViewBag.Categories = lookups.Categories;
            ViewBag.Authors = lookups.Authors;
            ViewBag.Publishers = lookups.Publishers;

            return View(book);
        }

        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = await api.PostAsJsonAsync("api/librarian/book", book);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Librarian API add book failed with status {StatusCode}", response.StatusCode);
            ModelState.AddModelError(string.Empty, "Не удалось добавить книгу через API.");
            return View(book);
        }

        return RedirectToAction(nameof(BookManagement));
    }

    public async Task<IActionResult> EditBook(int id)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var book = await api.GetFromJsonAsync<Book>($"api/librarian/book/{id}");

        if (book == null)
            return NotFound();

        var lookups = await api.GetFromJsonAsync<LibrarianBookFormLookupsResponse>("api/librarian/book-lookups")
            ?? new LibrarianBookFormLookupsResponse();
        ViewBag.Categories = lookups.Categories
            .Select(c => new SelectListItem
            {
                Value = c.CategoryID.ToString(),
                Text = c.CategoryName,
                Selected = c.CategoryID == book.CategoryID
            })
            .ToList();

        ViewBag.Authors = lookups.Authors;
        ViewBag.Publishers = lookups.Publishers;

        return View(book);
    }


    [HttpPost]
    public async Task<IActionResult> EditBook(Book book)
    {
        if (string.IsNullOrWhiteSpace(book.Title))
        {
            ModelState.AddModelError("", "�������� ����� �����������");
        }
        else if (book.Title.Length > 200)
        {
            ModelState.AddModelError("", "�������� ����� �� ������ ��������� 200 ��������");
        }

        if (book.CategoryID <= 0)
        {
            ModelState.AddModelError("", "���������� ������� ���������");
        }

        if (book.AuthorID <= 0)
        {
            ModelState.AddModelError("", "���������� ������� ������");
        }

        if (string.IsNullOrWhiteSpace(book.FilePath))
        {
            ModelState.AddModelError("", "���� � ����� ����������");
        }
        else if (book.FilePath.Length > 500)
        {
            ModelState.AddModelError("", "���� � ����� �� ������ ��������� 500 ��������");
        }

        if (book.PublishYear.HasValue && (book.PublishYear < 1000 || book.PublishYear > DateTime.Now.Year + 1))
        {
            ModelState.AddModelError("", $"��� ������� ������ ���� ����� 1000 � {DateTime.Now.Year + 1}");
        }

        if (book.Description != null && book.Description.Length > 2000)
        {
            ModelState.AddModelError("", "�������� �� ������ ��������� 2000 ��������");
        }

        if (!ModelState.IsValid)
        {
            var apiInvalid = _httpClientFactory.CreateClient("LibraryApi");
            var lookups = await apiInvalid.GetFromJsonAsync<LibrarianBookFormLookupsResponse>("api/librarian/book-lookups")
                ?? new LibrarianBookFormLookupsResponse();
            ViewBag.Categories = lookups.Categories
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.CategoryID.ToString(),
                    Text = c.CategoryName,
                    Selected = c.CategoryID == book.CategoryID
                })
                .ToList();

            ViewBag.Authors = lookups.Authors;
            ViewBag.Publishers = lookups.Publishers;

            return View(book);
        }

        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = await api.PutAsJsonAsync($"api/librarian/book/{book.BookID}", book);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Librarian API edit book failed with status {StatusCode}", response.StatusCode);
            ModelState.AddModelError(string.Empty, "Не удалось обновить книгу через API.");
            return View(book);
        }

        return RedirectToAction(nameof(BookManagement));
    }

    public async Task<IActionResult> DeleteBook(int id)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = await api.DeleteAsync($"api/librarian/book/{id}");
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Не удалось удалить книгу через API.";
        }

        return RedirectToAction(nameof(BookManagement));
    }

    public async Task<IActionResult> CategoryManagement(string search)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var categories = await api.GetFromJsonAsync<List<Category>>(
            $"api/librarian/categories?search={Uri.EscapeDataString(search ?? string.Empty)}") ?? new List<Category>();

        ViewBag.Search = search;
        return View(categories);
    }

    public IActionResult AddCategory() => View();

    [HttpPost]
    public IActionResult AddCategory(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.CategoryName))
        {
            ModelState.AddModelError("", "�������� ��������� �����������");
            return View(category);
        }

        if (category.CategoryName.Length > 100)
        {
            ModelState.AddModelError("", "�������� ��������� �� ������ ��������� 100 ��������");
            return View(category);
        }

        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = api.PostAsJsonAsync("api/librarian/categories", category).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Не удалось добавить категорию через API.");
            return View(category);
        }

        return RedirectToAction(nameof(CategoryManagement));
    }

    public async Task<IActionResult> DeleteCategory(int id)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        await api.DeleteAsync($"api/librarian/categories/{id}");

        return RedirectToAction(nameof(CategoryManagement));
    }
    public async Task<IActionResult> EditCategory(int id)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var category = await api.GetFromJsonAsync<Category>($"api/librarian/categories/{id}");

        if (category == null)
            return NotFound();

        return View(category);
    }

    [HttpPost]
    public IActionResult EditCategory(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.CategoryName))
        {
            ModelState.AddModelError("", "�������� ��������� �����������");
            return View(category);
        }

        if (category.CategoryName.Length > 100)
        {
            ModelState.AddModelError("", "�������� ��������� �� ������ ��������� 100 ��������");
            return View(category);
        }

        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = api.PutAsJsonAsync($"api/librarian/categories/{category.CategoryID}", category).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Не удалось обновить категорию через API.");
            return View(category);
        }

        return RedirectToAction(nameof(CategoryManagement));
    }

    public async Task<IActionResult> AuthorManagement(string search)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var authors = await api.GetFromJsonAsync<List<Author>>(
            $"api/librarian/authors?search={Uri.EscapeDataString(search ?? string.Empty)}") ?? new List<Author>();

        ViewBag.Search = search;
        return View(authors);
    }

    public IActionResult AddAuthor() => View();

    [HttpPost]
    public IActionResult AddAuthor(Author author)
    {
        if (string.IsNullOrWhiteSpace(author.FirstName))
        {
            ModelState.AddModelError("", "��� ������ �����������");
            return View(author);
        }

        if (string.IsNullOrWhiteSpace(author.LastName))
        {
            ModelState.AddModelError("", "������� ������ �����������");
            return View(author);
        }

        if (author.FirstName.Length > 50 || author.LastName.Length > 50)
        {
            ModelState.AddModelError("", "��� � ������� �� ������ ��������� 50 ��������");
            return View(author);
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(author.FirstName, @"^[A-Za-z�-��-���\s-]+$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(author.LastName, @"^[A-Za-z�-��-���\s-]+$"))
        {
            ModelState.AddModelError("", "��� � ������� ������ ��������� ������ �����, ������� � ������");
            return View(author);
        }

        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = api.PostAsJsonAsync("api/librarian/authors", author).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Не удалось добавить автора через API.");
            return View(author);
        }

        return RedirectToAction(nameof(AuthorManagement));
    }

    public async Task<IActionResult> EditAuthor(int id)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var author = await api.GetFromJsonAsync<Author>($"api/librarian/authors/{id}");

        if (author == null)
            return NotFound();

        return View(author);
    }

    [HttpPost]
    public IActionResult EditAuthor(Author author)
    {
        if (string.IsNullOrWhiteSpace(author.FirstName))
        {
            ModelState.AddModelError("", "��� ������ �����������");
            return View(author);
        }

        if (string.IsNullOrWhiteSpace(author.LastName))
        {
            ModelState.AddModelError("", "������� ������ �����������");
            return View(author);
        }

        if (author.FirstName.Length > 50 || author.LastName.Length > 50)
        {
            ModelState.AddModelError("", "��� � ������� �� ������ ��������� 50 ��������");
            return View(author);
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(author.FirstName, @"^[A-Za-z�-��-���\s-]+$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(author.LastName, @"^[A-Za-z�-��-���\s-]+$"))
        {
            ModelState.AddModelError("", "��� � ������� ������ ��������� ������ �����, ������� � ������");
            return View(author);
        }

        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = api.PutAsJsonAsync($"api/librarian/authors/{author.AuthorID}", author).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Не удалось обновить автора через API.");
            return View(author);
        }

        return RedirectToAction(nameof(AuthorManagement));
    }

    public async Task<IActionResult> DeleteAuthor(int id)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        await api.DeleteAsync($"api/librarian/authors/{id}");

        return RedirectToAction(nameof(AuthorManagement));
    }

    public async Task<IActionResult> PublisherManagement(string search)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var publishers = await api.GetFromJsonAsync<List<Publisher>>(
            $"api/librarian/publishers?search={Uri.EscapeDataString(search ?? string.Empty)}") ?? new List<Publisher>();

        ViewBag.Search = search;
        return View(publishers);
    }

    public IActionResult AddPublisher() => View();

    [HttpPost]
    public IActionResult AddPublisher(Publisher publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher.PublisherName))
        {
            ModelState.AddModelError("", "�������� ������������ �����������");
            return View(publisher);
        }

        if (publisher.PublisherName.Length > 200)
        {
            ModelState.AddModelError("", "�������� ������������ �� ������ ��������� 200 ��������");
            return View(publisher);
        }

        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = api.PostAsJsonAsync("api/librarian/publishers", publisher).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Не удалось добавить издательство через API.");
            return View(publisher);
        }

        return RedirectToAction(nameof(PublisherManagement));
    }

    public async Task<IActionResult> EditPublisher(int id)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var publisher = await api.GetFromJsonAsync<Publisher>($"api/librarian/publishers/{id}");

        if (publisher == null)
            return NotFound();

        return View(publisher);
    }

    [HttpPost]
    public IActionResult EditPublisher(Publisher publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher.PublisherName))
        {
            ModelState.AddModelError("", "�������� ������������ �����������");
            return View(publisher);
        }

        if (publisher.PublisherName.Length > 200)
        {
            ModelState.AddModelError("", "�������� ������������ �� ������ ��������� 200 ��������");
            return View(publisher);
        }

        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = api.PutAsJsonAsync($"api/librarian/publishers/{publisher.PublisherID}", publisher).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Не удалось обновить издательство через API.");
            return View(publisher);
        }

        return RedirectToAction(nameof(PublisherManagement));
    }

    public async Task<IActionResult> DeletePublisher(int id)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        await api.DeleteAsync($"api/librarian/publishers/{id}");

        return RedirectToAction(nameof(PublisherManagement));
    }

    public async Task<IActionResult> SubscriptionRequests(string? status)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var data = await api.GetFromJsonAsync<SubscriptionRequestsResponse>(
            $"api/librarian/subscription-requests?status={Uri.EscapeDataString(status ?? string.Empty)}")
            ?? new SubscriptionRequestsResponse();
        ViewBag.StatusFilter = data.StatusFilter;
        return View(data.Requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveSubscription(int subscriptionId)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = await api.PostAsync($"api/librarian/subscription-requests/{subscriptionId}/approve", null);
        var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
        if (response.IsSuccessStatusCode && payload?.Success == true)
        {
            TempData["Success"] = payload.Message;
        }
        else
        {
            TempData["Error"] = payload?.Message ?? "Не удалось одобрить заявку через API.";
        }
        return RedirectToAction(nameof(SubscriptionRequests));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectSubscription(int subscriptionId)
    {
        var api = _httpClientFactory.CreateClient("LibraryApi");
        var response = await api.PostAsync($"api/librarian/subscription-requests/{subscriptionId}/reject", null);
        var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
        if (response.IsSuccessStatusCode && payload?.Success == true)
        {
            TempData["Success"] = payload.Message;
        }
        else
        {
            TempData["Error"] = payload?.Message ?? "Не удалось отклонить заявку через API.";
        }
        return RedirectToAction(nameof(SubscriptionRequests));
    }

}
