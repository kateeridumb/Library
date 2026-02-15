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
    }
}
