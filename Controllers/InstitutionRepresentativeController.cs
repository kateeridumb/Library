using LibraryMPT.Extensions;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace LibraryMPT.Controllers
{
    [Authorize(Roles = "InstitutionRepresentative")]
    public class InstitutionRepresentativeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public InstitutionRepresentativeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var data = await api.GetFromJsonAsync<InstitutionIndexResponse>("api/institution/index")
                ?? new InstitutionIndexResponse { HasFaculty = false, ErrorMessage = "Ошибка API." };

            if (!data.HasFaculty)
                TempData["Error"] = data.ErrorMessage;

            ViewBag.Faculty = data.Faculty;
            ViewBag.FacultyId = data.FacultyId;
            ViewBag.ActiveSubscription = data.ActiveSubscription;
            ViewBag.TotalStudents = data.TotalStudents;
            ViewBag.TotalDownloads = data.TotalDownloads;
            ViewBag.TotalReads = data.TotalReads;
            return View();
        }

        public async Task<IActionResult> Subscriptions()
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var data = await api.GetFromJsonAsync<InstitutionSubscriptionsResponse>("api/institution/subscriptions")
                ?? new InstitutionSubscriptionsResponse { HasFaculty = false, ErrorMessage = "Ошибка API." };

            if (!data.HasFaculty)
            {
                TempData["Error"] = data.ErrorMessage;
                ViewBag.Faculty = null;
                ViewBag.MySubscriptions = new List<Subscription>();
                return View(new List<Subscription>());
            }

            ViewBag.Faculty = data.Faculty;
            ViewBag.MySubscriptions = data.MySubscriptions;
            return View(data.Templates);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectSubscription(int subscriptionId)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.PostAsJsonAsync("api/institution/select-subscription", new SelectSubscriptionRequest
            {
                SubscriptionId = subscriptionId
            });
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();

            if (payload?.Success == true)
                TempData["Success"] = payload.Message;
            else
                TempData["Error"] = payload?.Message ?? "Не удалось оформить подписку.";

            return RedirectToAction(nameof(Subscriptions));
        }

        public async Task<IActionResult> StudentStatistics(string? search, string? sortBy, string? sortDir)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var data = await api.GetFromJsonAsync<InstitutionStudentStatsResponse>(
                $"api/institution/student-statistics?search={Uri.EscapeDataString(search ?? string.Empty)}" +
                $"&sortBy={Uri.EscapeDataString(sortBy ?? string.Empty)}" +
                $"&sortDir={Uri.EscapeDataString(sortDir ?? string.Empty)}")
                ?? new InstitutionStudentStatsResponse { HasFaculty = false, ErrorMessage = "Ошибка API." };

            if (!data.HasFaculty)
            {
                TempData["Error"] = data.ErrorMessage;
                ViewBag.Faculty = null;
                return View(new List<StudentStatsDto>());
            }

            ViewBag.Faculty = data.Faculty;
            ViewBag.Search = data.Search;
            ViewBag.SortBy = data.SortBy;
            ViewBag.SortDir = data.SortDir;
            return View(data.Students);
        }

        public async Task<IActionResult> BookStatistics(string? search, string? sortBy, string? sortDir)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var data = await api.GetFromJsonAsync<InstitutionBookStatsResponse>(
                $"api/institution/book-statistics?search={Uri.EscapeDataString(search ?? string.Empty)}" +
                $"&sortBy={Uri.EscapeDataString(sortBy ?? string.Empty)}" +
                $"&sortDir={Uri.EscapeDataString(sortDir ?? string.Empty)}")
                ?? new InstitutionBookStatsResponse { HasFaculty = false, ErrorMessage = "Ошибка API." };

            if (!data.HasFaculty)
            {
                TempData["Error"] = data.ErrorMessage;
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Faculty = data.Faculty;
            ViewBag.Search = data.Search;
            ViewBag.SortBy = data.SortBy;
            ViewBag.SortDir = data.SortDir;
            return View(data.BookStats);
        }
    }
}

