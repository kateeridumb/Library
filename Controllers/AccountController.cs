using LibraryMPT.Models;
using LibraryMPT.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace LibraryMPT.Controllers
{
    public class AccountController : Controller
    {
        private readonly EmailService _emailService;
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(EmailService emailService, IHttpClientFactory httpClientFactory)
        {
            _emailService = emailService;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            ViewBag.Roles = await api.GetFromJsonAsync<List<Role>>("api/account/roles") ?? new List<Role>();
            return View();
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Пожалуйста, укажите email";
                return View();
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var result = await api.PostAsJsonAsync("api/account/forgot-password", new ForgotPasswordRequest { Email = email });
            var payload = await result.Content.ReadFromJsonAsync<ForgotPasswordResult>();
            if (payload?.UserExists == true && !string.IsNullOrWhiteSpace(payload.Token))
            {
                var resetLink = Url.Action("ResetPassword", "Account", new { token = payload.Token }, Request.Scheme, Request.Host.ToString());
                if (!string.IsNullOrEmpty(resetLink))
                {
                    try
                    {
                        await _emailService.SendPasswordResetEmailAsync(email, resetLink);
                    }
                    catch
                    {
                        TempData["Error"] = "Ошибка при отправке письма. Попробуйте позже или обратитесь к администратору.";
                        return View();
                    }
                }
            }

            TempData["Success"] = "Если указанный email зарегистрирован в системе, вы получите инструкции по восстановлению пароля.";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Некорректный токен для восстановления пароля";
                return RedirectToAction("Login");
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var result = await api.GetFromJsonAsync<ApiCommandResponse>($"api/account/validate-reset-token?token={Uri.EscapeDataString(token)}");
            if (result?.Success != true)
            {
                TempData["Error"] = "Ссылка для восстановления пароля недействительна или устарела. Запросите новую ссылку.";
                return RedirectToAction("ForgotPassword");
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string token, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Некорректный токен для восстановления пароля";
                return RedirectToAction("Login");
            }
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                TempData["Error"] = "Пожалуйста, заполните все поля";
                ViewBag.Token = token;
                return View();
            }
            if (password != confirmPassword)
            {
                TempData["Error"] = "Пароли не совпадают";
                ViewBag.Token = token;
                return View();
            }
            if (!IsPasswordValid(password, out var error))
            {
                TempData["Error"] = error;
                ViewBag.Token = token;
                return View();
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.PostAsJsonAsync("api/account/reset-password", new ResetPasswordRequest
            {
                Token = token,
                Password = password
            });
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            if (payload?.Success != true)
            {
                TempData["Error"] = payload?.Message ?? "Не удалось изменить пароль.";
                return RedirectToAction("ForgotPassword");
            }

            TempData["Success"] = "Пароль успешно изменен. Теперь вы можете войти в систему.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Register(string firstName, string lastName, string email, string username, string password, bool personalDataConsent)
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            ViewBag.Roles = await api.GetFromJsonAsync<List<Role>>("api/account/roles") ?? new List<Role>();

            if (!personalDataConsent)
            {
                ModelState.AddModelError("", "Необходимо согласие на обработку персональных данных");
                return View();
            }
            if (!Regex.IsMatch(firstName, @"^[A-Za-zА-Яа-яЁё-]+$") || !Regex.IsMatch(lastName, @"^[A-Za-zА-Яа-яЁё-]+$"))
            {
                ModelState.AddModelError("", "Имя и фамилия должны содержать только буквы");
                return View();
            }
            if (!email.EndsWith("@gmail.com"))
            {
                ModelState.AddModelError("", "Почта должна заканчиваться на @gmail.com");
                return View();
            }
            if (!IsPasswordValid(password, out var error))
            {
                ModelState.AddModelError("", error);
                return View();
            }

            var response = await api.PostAsJsonAsync("api/account/register", new AccountRegisterRequest
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Username = username,
                Password = password
            });
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            if (payload?.Success != true)
            {
                ModelState.AddModelError("", payload?.Message ?? "Не удалось зарегистрироваться.");
                return View();
            }

            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Логин и пароль обязательны");
                return View();
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var response = await api.PostAsJsonAsync("api/account/login", new AccountLoginRequest
            {
                Username = username,
                Password = password
            });
            var payload = await response.Content.ReadFromJsonAsync<AccountLoginResult>();

            if (payload?.Success != true)
            {
                ModelState.AddModelError("", payload?.Error ?? "Неверный логин или пароль");
                return View();
            }

            if (payload.RequiresTwoFactor && payload.RoleName == "Student")
            {
                var code = GenerateTwoFactorCode();
                await api.PostAsJsonAsync("api/account/set-twofactor-code", new SetTwoFactorCodeRequest
                {
                    TwoFactorToken = payload.TwoFactorToken ?? string.Empty,
                    Code = code,
                    ExpiryUtc = DateTime.UtcNow.AddMinutes(10)
                });

                try
                {
                    await _emailService.SendTwoFactorCodeEmailAsync(payload.Email, code, payload.FirstName);
                }
                catch
                {
                    ModelState.AddModelError("", "Ошибка при отправке кода подтверждения. Попробуйте позже.");
                    return View();
                }

                HttpContext.Session.SetString("2FA_Token", payload.TwoFactorToken ?? string.Empty);
                HttpContext.Session.SetString("2FA_UserId", payload.UserId.ToString());
                HttpContext.Session.SetString("2FA_Username", payload.Username);
                HttpContext.Session.SetString("2FA_Role", payload.RoleName);
                return RedirectToAction("VerifyTwoFactor");
            }

            await SignInUserAsync(payload.UserId, payload.Username, payload.RoleName);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuestLogin()
        {
            var api = _httpClientFactory.CreateClient("LibraryApi");
            var payload = await (await api.PostAsync("api/account/guest-login", null)).Content.ReadFromJsonAsync<GuestLoginResult>();
            if (payload?.Success != true)
                return RedirectToAction("Login");

            await SignInUserAsync(payload.UserId, "Гость", "Guest");
            return RedirectToAction("Index", "Client");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult VerifyTwoFactor()
        {
            var token = HttpContext.Session.GetString("2FA_Token");
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Сессия истекла. Пожалуйста, войдите снова.";
                return RedirectToAction("Login");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyTwoFactor(string code)
        {
            var twoFactorToken = HttpContext.Session.GetString("2FA_Token");
            var userIdStr = HttpContext.Session.GetString("2FA_UserId");
            var username = HttpContext.Session.GetString("2FA_Username");
            var role = HttpContext.Session.GetString("2FA_Role");
            if (string.IsNullOrEmpty(twoFactorToken) || string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role))
            {
                TempData["Error"] = "Сессия истекла. Пожалуйста, войдите снова.";
                return RedirectToAction("Login");
            }
            if (string.IsNullOrWhiteSpace(code))
            {
                ModelState.AddModelError("", "Пожалуйста, введите код подтверждения");
                return View();
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var verify = await (await api.PostAsJsonAsync("api/account/verify-twofactor-code", new TwoFactorCodeRequest
            {
                TwoFactorToken = twoFactorToken,
                Code = code
            })).Content.ReadFromJsonAsync<ApiCommandResponse>();

            if (verify?.Success != true)
            {
                ModelState.AddModelError("", "Неверный или устаревший код подтверждения");
                return View();
            }

            await api.PostAsJsonAsync("api/account/clear-twofactor-code", new TwoFactorCodeRequest { TwoFactorToken = twoFactorToken, Code = code });

            HttpContext.Session.Remove("2FA_Token");
            HttpContext.Session.Remove("2FA_UserId");
            HttpContext.Session.Remove("2FA_Username");
            HttpContext.Session.Remove("2FA_Role");

            var userId = int.Parse(userIdStr);

            await SignInUserAsync(userId, username, role);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableTwoFactor()
        {
            if (!User.IsInRole("Student"))
            {
                TempData["Error"] = "Двухфакторная аутентификация доступна только для студентов";
                return RedirectToAction("Index", "Home");
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                TempData["Error"] = "Ошибка идентификации пользователя";
                return RedirectToAction("Index", "Home");
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            var payload = await (await api.PostAsJsonAsync("api/account/toggle-twofactor", new ToggleTwoFactorRequest
            {
                Enabled = true
            })).Content.ReadFromJsonAsync<ApiCommandResponse>();

            if (payload?.Success == true)
                TempData["Success"] = "Двухфакторная аутентификация успешно включена";
            else
                TempData["Error"] = payload?.Message ?? "Не удалось включить 2FA";

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTwoFactor()
        {
            if (!User.IsInRole("Student"))
            {
                TempData["Error"] = "Двухфакторная аутентификация доступна только для студентов";
                return RedirectToAction("Index", "Home");
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                TempData["Error"] = "Ошибка идентификации пользователя";
                return RedirectToAction("Index", "Home");
            }

            var api = _httpClientFactory.CreateClient("LibraryApi");
            await api.PostAsJsonAsync("api/account/toggle-twofactor", new ToggleTwoFactorRequest
            {
                Enabled = false
            });

            TempData["Success"] = "Двухфакторная аутентификация успешно отключена";
            return RedirectToAction("Index", "Home");
        }

        private async Task SignInUserAsync(int userId, string username, string roleName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, roleName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        private static string GenerateTwoFactorCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private static bool IsPasswordValid(string password, out string error)
        {
            if (password.Length < 12) { error = "Пароль должен быть не менее 12 символов"; return false; }
            if (!password.Any(char.IsUpper)) { error = "Пароль должен содержать хотя бы одну заглавную букву"; return false; }
            if (!password.Any(char.IsLower)) { error = "Пароль должен содержать хотя бы одну строчную букву"; return false; }
            if (!password.Any(char.IsDigit)) { error = "Пароль должен содержать хотя бы одну цифру"; return false; }
            error = null!;
            return true;
        }
    }
}

