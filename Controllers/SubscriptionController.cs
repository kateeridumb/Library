using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace LibraryMPT.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SubscriptionController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(IHttpClientFactory httpClientFactory, ILogger<SubscriptionController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var subscriptions = await api.GetFromJsonAsync<List<Subscription>>("api/subscriptions")
                ?? new List<Subscription>();

            ViewBag.Templates = subscriptions.Where(s => !s.FacultyID.HasValue).ToList();
            ViewBag.ActiveSubscriptions = subscriptions.Where(s => s.FacultyID.HasValue).ToList();

            return View(subscriptions);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Subscription subscription)
        {



            if (string.IsNullOrWhiteSpace(subscription.Name))
            {
                ModelState.AddModelError("", "Название подписки обязательно");
            }
            else if (subscription.Name.Length > 200)
            {
                ModelState.AddModelError("", "Название подписки не должно превышать 200 символов");
            }

            if (!subscription.DurationDays.HasValue || subscription.DurationDays.Value <= 0)
            {
                ModelState.AddModelError("", "Длительность подписки в днях обязательна и должна быть больше 0");
            }

            if (!ModelState.IsValid)
            {
                return View(subscription);
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.PostAsJsonAsync("api/subscriptions/template", subscription);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Subscriptions API create template failed with status {StatusCode}", response.StatusCode);
                ModelState.AddModelError(string.Empty, "Не удалось создать шаблон подписки через API.");
                return View(subscription);
            }

            TempData["Success"] = "Шаблон подписки успешно создан";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var subscription = await api.GetFromJsonAsync<Subscription>($"api/subscriptions/{id}");

            if (subscription == null)
                return NotFound();

            return View(subscription);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.DeleteAsync($"api/subscriptions/{id}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Subscriptions API delete failed with status {StatusCode}", response.StatusCode);
                TempData["Error"] = "Не удалось удалить подписку через API.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "Подписка удалена";
            return RedirectToAction(nameof(Index));
        }
    }
}

