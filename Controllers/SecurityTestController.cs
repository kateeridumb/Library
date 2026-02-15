using Microsoft.AspNetCore.Mvc;

namespace LibraryMPT.Controllers
{
    /// <summary>
    /// Контроллер для тестирования защит безопасности
    /// Доступен только в режиме Development
    /// </summary>
    public class SecurityTestController : Controller
    {
        private readonly IWebHostEnvironment _environment;

        public SecurityTestController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Проверяем, что мы в Development режиме
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            return View();
        }
    }
}

