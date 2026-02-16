using LibraryMPT.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace LibraryMPT.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var data = await api.GetFromJsonAsync<HomeIndexResponse>("api/home/index")
                ?? new HomeIndexResponse();

            ViewBag.TotalUsers = data.TotalUsers;
            ViewBag.TotalBooks = data.TotalBooks;
            ViewBag.Downloads = data.Downloads;
            ViewBag.Availability = data.Availability;
            if (data.IsTwoFactorEnabled.HasValue)
            {
                ViewBag.IsTwoFactorEnabled = data.IsTwoFactorEnabled.Value;
            }

            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Error(int? statusCode = null, string? message = null)
        {
            var status = statusCode ?? 500;
            ViewBag.StatusCode = status;
            ViewBag.RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            ViewBag.ErrorTitle = status switch
            {
                404 => "Страница не найдена",
                403 => "Доступ запрещен",
                500 => "Внутренняя ошибка сервера",
                _ => "Произошла ошибка"
            };
            ViewBag.ErrorMessage = !string.IsNullOrWhiteSpace(message)
                ? message
                : (status switch
                {
                    404 => "Запрашиваемая страница не существует.",
                    403 => "У вас нет доступа к этому ресурсу.",
                    500 => "Произошла внутренняя ошибка сервера. Пожалуйста, попробуйте позже.",
                    _ => "Произошла непредвиденная ошибка."
                });
            return View();
        }
    }
}
