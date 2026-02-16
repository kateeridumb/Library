using LibraryMPT.Extensions;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LibraryMPT.Controllers
{
    [Authorize(Roles = "Student,Admin,Librarian,InstitutionRepresentative,Guest")]
    public class ClientController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ClientController> _logger;

        public ClientController(IHttpClientFactory httpClientFactory, ILogger<ClientController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? search, int? categoryId)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var requestUri = string.IsNullOrWhiteSpace(search)
                ? "api/client/index"
                : $"api/client/index?search={Uri.EscapeDataString(search)}";
            if (categoryId.HasValue)
            {
                requestUri += requestUri.Contains('?')
                    ? $"&categoryId={categoryId.Value}"
                    : $"?categoryId={categoryId.Value}";
            }

            ClientIndexResponse data;
            try
            {
                data = await api.GetFromJsonAsync<ClientIndexResponse>(requestUri)
                    ?? new ClientIndexResponse();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Client index API request failed");
                TempData["Error"] = "Не удалось загрузить список книг. Попробуйте еще раз.";
                data = new ClientIndexResponse();
            }

            ViewBag.Categories = data.Categories;
            ViewBag.HasSubscription = data.HasSubscription;
            ViewBag.SubscriptionStatus = data.SubscriptionStatus;
            ViewBag.ReadedBookIds = data.ReadedBookIds;
            ViewBag.TotalBooks = data.TotalBooks;
            ViewBag.Readed = data.Readed;
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;

            return View(data.Books);
        }

        public async Task<IActionResult> BookDetails(int id)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            ClientBookDetailsResponse? data = null;
            try
            {
                data = await api.GetFromJsonAsync<ClientBookDetailsResponse>($"api/client/book-details/{id}");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Client book-details API request failed for bookId={BookId}", id);
                TempData["Error"] = "Не удалось загрузить информацию о книге.";
                return RedirectToAction(nameof(Index));
            }
            if (data?.Book == null)
                return NotFound();

            ViewBag.HasSubscription = data.HasSubscription;
            return View(data.Book);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int bookId)
        {
            if (User.IsInRole("Guest"))
                return Forbid();

            var userId = User.GetUserId();
            var api = _httpClientFactory.CreateClient("LibraryApi");
            await api.PostAsJsonAsync("api/client/mark-read", new MarkAsReadRequest
            {
                UserId = userId,
                BookId = bookId
            });

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ReadOnline(int id)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            ClientReadOnlineResponse? data = null;
            try
            {
                data = await api.GetFromJsonAsync<ClientReadOnlineResponse>($"api/client/read-online/{id}");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Client read-online API request failed for bookId={BookId}", id);
                TempData["Error"] = "Не удалось открыть книгу для чтения.";
                return RedirectToAction(nameof(BookDetails), new { id });
            }
            if (data?.Book == null)
                return NotFound();
            if (string.IsNullOrWhiteSpace(data.FilePath))
            {
                TempData["Error"] = "Файл книги не найден.";
                return RedirectToAction("BookDetails", new { id });
            }
            if (!data.CanRead)
            {
                TempData["Error"] = "Эта книга доступна только по подписке.";
                return RedirectToAction("BookDetails", new { id });
            }

            ViewBag.HasSubscription = data.HasSubscription;
            ViewBag.CanRead = data.CanRead;
            ViewBag.FilePath = data.FilePath;
            ViewBag.FileType = data.FileType;
            ViewBag.FileUrl = data.FileType == "epub" || data.FileType == "fb2"
                ? Url.Action("GetReaderFile", "Client", new { id })
                : Url.Action("GetBookFile", "Client", new { id });

            return View(data.Book);
        }

        public class ReaderErrorDto
        {
            public int BookId { get; set; }
            public string? FileType { get; set; }
            public string? FileUrl { get; set; }
            public string? Stage { get; set; }
            public string? Message { get; set; }
            public string? Detail { get; set; }
        }

        [HttpPost]
        public IActionResult LogReaderError([FromBody] ReaderErrorDto dto)
        {
            _logger.LogWarning(
                "Reader error. BookId={BookId}, FileType={FileType}, FileUrl={FileUrl}, Stage={Stage}, Message={Message}, Detail={Detail}",
                dto.BookId, dto.FileType, dto.FileUrl, dto.Stage, dto.Message, dto.Detail);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetBookFile(int id)
        {
            return await ProxyFileFromApiAsync($"api/client/file/{id}");
        }

        [HttpGet]
        public async Task<IActionResult> GetReaderFile(int id)
        {
            return await ProxyFileFromApiAsync($"api/client/file/{id}");
        }

        public async Task<IActionResult> Download(int id)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.GetAsync($"api/client/download/{id}", HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
                    TempData["Error"] = payload?.Message ?? "Не удалось скачать файл.";
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "Недостаточно прав для скачивания файла.";
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    TempData["Error"] = "Файл не найден.";
                }
                else
                {
                    TempData["Error"] = $"Ошибка загрузки файла ({(int)response.StatusCode}).";
                }

                return RedirectToAction(nameof(BookDetails), new { id });
            }

            HttpContext.Response.RegisterForDispose(response);
            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName;
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                fileName = fileName.Trim('"');
            }

            var result = string.IsNullOrWhiteSpace(fileName)
                ? File(stream, contentType)
                : File(stream, contentType, fileName);
            result.EnableRangeProcessing = true;
            return result;
        }

        public async Task<IActionResult> Readed(string? search)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var data = await api.GetFromJsonAsync<ClientReadedResponse>(
                $"api/client/readed?search={Uri.EscapeDataString(search ?? string.Empty)}")
                ?? new ClientReadedResponse();

            ViewBag.Search = search;
            ViewBag.IsReadedPage = true;
            return View(data.Books);
        }

        [HttpGet]
        public async Task<IActionResult> GetBookmarks(int bookId)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var bookmarks = await api.GetFromJsonAsync<List<Bookmark>>($"api/client/bookmarks?bookId={bookId}")
                ?? new List<Bookmark>();
            return Json(bookmarks);
        }

        [HttpPost]
        public async Task<IActionResult> AddBookmark([FromBody] BookmarkDto bookmarkDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.PostAsJsonAsync("api/client/bookmarks", new ClientBookmarkRequest
            {
                Bookmark = bookmarkDto
            });
            var payload = await ReadApiCommandResponseSafeAsync(response);
            return Json(new { success = response.IsSuccessStatusCode && payload?.Success == true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBookmark(int bookmarkId)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.DeleteAsync($"api/client/bookmarks/{bookmarkId}");
            var payload = await ReadApiCommandResponseSafeAsync(response);
            if (payload?.Success != true)
                return NotFound();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBookmark([FromBody] BookmarkDto bookmarkDto)
        {
            if (!ModelState.IsValid || bookmarkDto.BookmarkID == 0)
                return BadRequest(ModelState);

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.PutAsJsonAsync("api/client/bookmarks", new ClientBookmarkRequest
            {
                Bookmark = bookmarkDto
            });
            var payload = await ReadApiCommandResponseSafeAsync(response);
            if (payload?.Success != true)
                return NotFound();

            return Json(new { success = true });
        }

        private async Task<IActionResult> ProxyFileFromApiAsync(string requestUri)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }

            HttpContext.Response.RegisterForDispose(response);
            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var result = File(stream, contentType);
            result.EnableRangeProcessing = true;
            return result;
        }

        private static async Task<ApiCommandResponse?> ReadApiCommandResponseSafeAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!IsJsonContent(response.Content.Headers.ContentType))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ApiCommandResponse>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static bool IsJsonContent(MediaTypeHeaderValue? contentType)
        {
            var mediaType = contentType?.MediaType;
            return !string.IsNullOrWhiteSpace(mediaType) &&
                   mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
        }
    }
}

