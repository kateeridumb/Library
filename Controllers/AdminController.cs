using LibraryMPT.Models;
using LibraryMPT.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net.Http.Json;

namespace LibraryMPT.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly EmailService _emailService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            EmailService emailService,
            IHttpClientFactory httpClientFactory,
            ILogger<AdminController> logger)
        {
            _emailService = emailService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            AdminDashboardStatsDto stats;
            try
            {
                var apiClient = _httpClientFactory.CreateClient("LibraryApi");
                stats = await apiClient.GetFromJsonAsync<AdminDashboardStatsDto>("api/admin/stats")
                    ?? new AdminDashboardStatsDto();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load admin stats from API.");
                stats = new AdminDashboardStatsDto();
                TempData["Error"] = "Не удалось загрузить данные панели администратора.";
            }

            ViewBag.TotalUsers = stats.TotalUsers;
            ViewBag.AdminCount = stats.AdminCount;
            ViewBag.LibrarianCount = stats.LibrarianCount;
            ViewBag.ReaderCount = stats.ReaderCount;

            return View();
        }

        /// <summary>
        /// Панель мониторинга безопасности и производительности для администратора
        /// </summary>
        public async Task<IActionResult> SecurityDashboard()
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var data = await apiClient.GetFromJsonAsync<AdminSecurityDashboardResponse>("api/admin/security-dashboard")
                ?? new AdminSecurityDashboardResponse();
            ViewBag.TotalUsers = data.TotalUsers;
            ViewBag.TotalBooks = data.TotalBooks;
            ViewBag.DownloadsLast24h = data.DownloadsLast24h;
            ViewBag.ReadsLast24h = data.ReadsLast24h;
            ViewBag.AuditEventsLast24h = data.AuditEventsLast24h;
            ViewBag.BlockedUsers = data.BlockedUsers;
            ViewBag.TwoFactorUsers = data.TwoFactorUsers;
            ViewBag.TwoFactorStudents = data.TwoFactorStudents;
            ViewBag.ActiveSubscriptions = data.ActiveSubscriptions;
            ViewBag.PendingSubscriptions = data.PendingSubscriptions;
            ViewBag.BooksRequiringSubscription = data.BooksRequiringSubscription;
            ViewBag.DbSizeMb = data.DbSizeMb;
            ViewBag.LastAudit = data.LastAudit;
            ViewBag.AuditPopular = data.AuditPopular;

            return View();
        }

        /// <summary>
        /// Простые «живые» метрики с сервера для AJAX-обновления дашборда
        /// </summary>
        [HttpGet]
        public IActionResult RuntimeMetrics()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();

            var data = new
            {
                serverTimeUtc = DateTime.UtcNow,
                workingSetBytes = process.WorkingSet64,
                gcTotalMemoryBytes = GC.GetTotalMemory(false),
                processId = process.Id,
                emailLastDurationMs = EmailService.LastSendDurationMs,
                emailLastSendUtc = EmailService.LastSendUtc,
                emailLastError = EmailService.LastSendError
            };

            return Json(data);
        }

        public async Task<IActionResult> UserManagement(string search, string roleFilter, string facultyFilter, string statusFilter)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var data = await apiClient.GetFromJsonAsync<AdminUserManagementResponse>(
                $"api/admin/user-management?search={Uri.EscapeDataString(search ?? string.Empty)}" +
                $"&roleFilter={Uri.EscapeDataString(roleFilter ?? string.Empty)}" +
                $"&facultyFilter={Uri.EscapeDataString(facultyFilter ?? string.Empty)}" +
                $"&statusFilter={Uri.EscapeDataString(statusFilter ?? string.Empty)}")
                ?? new AdminUserManagementResponse();

            ViewBag.Search = data.Search;
            ViewBag.RoleFilter = data.RoleFilter;
            ViewBag.FacultyFilter = data.FacultyFilter;
            ViewBag.StatusFilter = data.StatusFilter;
            ViewBag.Roles = data.Roles;
            ViewBag.Faculties = data.Faculties;
            ViewBag.Users = data.Users;
            return View(data.Users);
        }

        [HttpGet]
        public async Task<IActionResult> DecryptLastName(int userId)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var data = await apiClient.GetFromJsonAsync<DecryptLastNameResponse>($"api/admin/decrypt-last-name/{userId}")
                ?? new DecryptLastNameResponse { Success = false, Error = "API error." };
            return Json(new { success = data.Success, lastName = data.LastName, error = data.Error });
        }


        public async Task<IActionResult> RoleAssignment()
        {
            ViewBag.CurrentUserId = int.Parse(
    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value
            );

            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var data = await apiClient.GetFromJsonAsync<AdminRoleAssignmentResponse>("api/admin/role-assignment")
                ?? new AdminRoleAssignmentResponse();
            ViewBag.Roles = data.Roles;
            return View(data.Users);
        }


        public async Task<IActionResult> AuditLog(string actionType, string? search, string? sortBy, string? sortDir)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var data = await apiClient.GetFromJsonAsync<AdminAuditLogResponse>(
                $"api/admin/audit-log?actionType={Uri.EscapeDataString(actionType ?? string.Empty)}" +
                $"&search={Uri.EscapeDataString(search ?? string.Empty)}" +
                $"&sortBy={Uri.EscapeDataString(sortBy ?? string.Empty)}" +
                $"&sortDir={Uri.EscapeDataString(sortDir ?? string.Empty)}")
                ?? new AdminAuditLogResponse();
            ViewBag.ActionType = data.ActionType;
            ViewBag.Search = data.Search;
            ViewBag.SortBy = data.SortBy;
            ViewBag.SortDir = data.SortDir;
            return View(data.Logs);
        }


        public IActionResult DatabaseBackup()
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var data = apiClient.GetFromJsonAsync<AdminBackupResponse>("api/admin/backups")
                .GetAwaiter().GetResult() ?? new AdminBackupResponse();
            ViewBag.BackupFiles = data.BackupFiles
                .Select(f => (f.Name, f.Date, f.Size, Path.Combine(data.BackupDir, f.Name)))
                .ToList();
            ViewBag.BackupDir = data.BackupDir;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBackup()
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.PostAsync("api/admin/backups/create", null);
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            TempData[payload?.Success == true ? "Success" : "Error"] = payload?.Message ?? "Ошибка создания бэкапа.";

            return RedirectToAction(nameof(DatabaseBackup));
        }

        public IActionResult DownloadBackup(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return NotFound();
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var uri = new Uri(apiClient.BaseAddress!, $"api/admin/backups/download?fileName={Uri.EscapeDataString(fileName)}");
            return Redirect(uri.ToString());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteBackup(string fileName)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = apiClient.DeleteAsync($"api/admin/backups?fileName={Uri.EscapeDataString(fileName ?? string.Empty)}")
                .GetAwaiter().GetResult();
            var payload = response.Content.ReadFromJsonAsync<ApiCommandResponse>().GetAwaiter().GetResult();
            TempData[payload?.Success == true ? "Success" : "Error"] = payload?.Message ?? "Ошибка удаления бэкапа.";
            return RedirectToAction(nameof(DatabaseBackup));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreBackup(string fileName)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.PostAsync($"api/admin/backups/restore?fileName={Uri.EscapeDataString(fileName ?? string.Empty)}", null);
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            TempData[payload?.Success == true ? "Success" : "Error"] = payload?.Message ?? "Ошибка восстановления.";

            return RedirectToAction(nameof(DatabaseBackup));
        }

        public async Task<IActionResult> CreateUser()
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var lookups = await apiClient.GetFromJsonAsync<CreateUserLookupsResponse>("api/admin/create-user-lookups")
                ?? new CreateUserLookupsResponse();
            ViewBag.Roles = lookups.Roles;
            ViewBag.Faculties = lookups.Faculties;

            return View();
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(User user)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            await apiClient.PostAsJsonAsync("api/admin/users", user);

            return RedirectToAction(nameof(UserManagement));
        }

        public async Task<IActionResult> EditUser(int id)
        {
            var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var data = await apiClient.GetFromJsonAsync<EditUserViewResponse>($"api/admin/users/{id}/edit");
            if (data?.User == null) return NotFound();
            ViewBag.DecryptedLastName = data.DecryptedLastName;
            ViewBag.Roles = data.Roles;
            ViewBag.Faculties = data.Faculties;
            ViewBag.CanEditFaculty = data.CanEditFaculty;
            return View(data.User);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(UserAdminDto dto)
        {
            var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.PutAsJsonAsync($"api/admin/users/{dto.UserID}", new UpdateUserRequest
            {
                Dto = dto
            });
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            TempData[payload?.Success == true ? "Success" : "Error"] = payload?.Message ?? "Ошибка обновления пользователя.";

            return RedirectToAction(nameof(UserManagement));
        }


        [HttpPost]
        public async Task<IActionResult> ToggleBlock(int id)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            await apiClient.PostAsync($"api/admin/users/{id}/toggle-block", null);

            return RedirectToAction(nameof(UserManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.DeleteAsync($"api/admin/users/{id}");
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            if (payload?.Success == true)
            {
                TempData["Success"] = "Пользователь удален.";
            }
            else if (!string.IsNullOrWhiteSpace(payload?.Message))
            {
                TempData["Error"] = payload.Message;
            }
            return RedirectToAction(nameof(UserManagement));
        }

        public async Task<IActionResult> CreateStaff()
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var lookups = await apiClient.GetFromJsonAsync<CreateUserLookupsResponse>("api/admin/create-staff-lookups")
                ?? new CreateUserLookupsResponse();
            ViewBag.Roles = lookups.Roles;
            ViewBag.Faculties = lookups.Faculties;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(StaffCreateDto dto)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(dto.FirstName, @"^[A-Za-zА-Яа-яЁё]+$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(dto.LastName, @"^[A-Za-zА-Яа-яЁё]+$"))
            {
                ModelState.AddModelError("", "Имя и фамилия должны содержать только буквы");
            }

            if (!dto.Email.EndsWith("@gmail.com"))
            {
                ModelState.AddModelError("", "Почта должна заканчиваться на @gmail.com");
            }

            if (!ModelState.IsValid)
            {
                var apiLookups = _httpClientFactory.CreateClient("LibraryApi");
                var lookups = await apiLookups.GetFromJsonAsync<CreateUserLookupsResponse>("api/admin/create-staff-lookups")
                    ?? new CreateUserLookupsResponse();
                ViewBag.Roles = lookups.Roles;
                ViewBag.Faculties = lookups.Faculties;
                return View(dto);
            }

            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.PostAsJsonAsync("api/admin/staff", new CreateStaffRequest { Dto = dto });
            var result = await response.Content.ReadFromJsonAsync<CreateStaffResult>();
            if (result?.Success != true)
            {
                TempData["Error"] = result?.Message ?? "Не удалось создать сотрудника.";
                return RedirectToAction(nameof(UserManagement));
            }

            try
            {
                var loginUrl = Url.Action("Login", "Account", null, Request.Scheme, Request.Host.ToString());
                if (string.IsNullOrEmpty(loginUrl))
                {
                    TempData["Warning"] = $"Пользователь создан, но не удалось отправить письмо. Пароль: {result.GeneratedPassword}";
                    return RedirectToAction(nameof(UserManagement));
                }
                await _emailService.SendStaffRegistrationEmailAsync(
                    dto.Email,
                    dto.FirstName,
                    dto.LastName,
                    dto.Username,
                    result.GeneratedPassword ?? string.Empty,
                    result.RoleName ?? "Сотрудник",
                    loginUrl
                );
            }
            catch (Exception ex)
            {
                TempData["Warning"] = $"Пользователь создан, но не удалось отправить письмо: {ex.Message}. Пароль: {result.GeneratedPassword}";
                return RedirectToAction(nameof(UserManagement));
            }

            TempData["Success"] = "Сотрудник успешно создан. Данные для входа отправлены на email.";
            return RedirectToAction(nameof(UserManagement));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole(int userId, int roleId)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            await apiClient.PostAsJsonAsync("api/admin/users/update-role", new UpdateUserRoleRequest
            {
                UserId = userId,
                RoleId = roleId
            });

            return RedirectToAction(nameof(RoleAssignment));
        }
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAuditLogs()
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            await apiClient.PostAsync("api/admin/clear-audit-logs", null);

            return RedirectToAction(nameof(AuditLog));
        }

        public async Task<IActionResult> FacultyManagement(string search)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var faculties = await apiClient.GetFromJsonAsync<List<Faculty>>(
                $"api/admin/faculties?search={Uri.EscapeDataString(search ?? string.Empty)}")
                ?? new List<Faculty>();

            ViewBag.Search = search;
            return View(faculties);
        }

        public IActionResult AddFaculty() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFaculty(Faculty faculty)
        {
            if (string.IsNullOrWhiteSpace(faculty.FacultyName))
            {
                ModelState.AddModelError("", "�������� ���������� �����������");
                return View(faculty);
            }

            if (faculty.FacultyName.Length > 200)
            {
                ModelState.AddModelError("", "�������� ���������� �� ������ ��������� 200 ��������");
                return View(faculty);
            }

            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.PostAsJsonAsync("api/admin/faculties", faculty);
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            if (payload?.Success != true)
            {
                ModelState.AddModelError("", payload?.Message ?? "Не удалось добавить факультет.");
                return View(faculty);
            }

            TempData["Success"] = "Факультет успешно добавлен.";
            return RedirectToAction(nameof(FacultyManagement));
        }

        public async Task<IActionResult> EditFaculty(int id)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var faculty = await apiClient.GetFromJsonAsync<Faculty>($"api/admin/faculties/{id}");

            if (faculty == null)
                return NotFound();

            return View(faculty);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFaculty(Faculty faculty)
        {
            if (string.IsNullOrWhiteSpace(faculty.FacultyName))
            {
                ModelState.AddModelError("", "�������� ���������� �����������");
                return View(faculty);
            }

            if (faculty.FacultyName.Length > 200)
            {
                ModelState.AddModelError("", "�������� ���������� �� ������ ��������� 200 ��������");
                return View(faculty);
            }

            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.PutAsJsonAsync($"api/admin/faculties/{faculty.FacultyID}", faculty);
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            if (payload?.Success != true)
            {
                ModelState.AddModelError("", payload?.Message ?? "Не удалось обновить факультет.");
                return View(faculty);
            }

            TempData["Success"] = "Факультет успешно обновлен.";
            return RedirectToAction(nameof(FacultyManagement));
        }

        public async Task<IActionResult> DeleteFaculty(int id)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var faculty = await apiClient.GetFromJsonAsync<Faculty>($"api/admin/faculties/{id}");

            if (faculty == null)
                return NotFound();

            return View(faculty);
        }

        [HttpPost, ActionName("DeleteFaculty")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFacultyConfirmed(int id)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            await apiClient.DeleteAsync($"api/admin/faculties/{id}");
            TempData["Success"] = "Факультет удален.";
            return RedirectToAction(nameof(FacultyManagement));
        }

        public async Task<IActionResult> RoleManagement(string search)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var roles = await apiClient.GetFromJsonAsync<List<Role>>(
                $"api/admin/roles?search={Uri.EscapeDataString(search ?? string.Empty)}")
                ?? new List<Role>();

            ViewBag.Search = search;
            return View(roles);
        }

        public IActionResult AddRole() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRole(Role role)
        {
            if (string.IsNullOrWhiteSpace(role.RoleName))
            {
                ModelState.AddModelError("", "�������� ���� �����������");
                return View(role);
            }

            if (role.RoleName.Length > 50)
            {
                ModelState.AddModelError("", "�������� ���� �� ������ ��������� 50 ��������");
                return View(role);
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(role.RoleName, @"^[A-Za-z�-��-���0-9\s]+$"))
            {
                ModelState.AddModelError("", "�������� ���� ������ ��������� ������ �����, ����� � �������");
                return View(role);
            }

            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.PostAsJsonAsync("api/admin/roles", role);
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            if (payload?.Success != true)
            {
                ModelState.AddModelError("", payload?.Message ?? "Не удалось добавить роль.");
                return View(role);
            }

            TempData["Success"] = "Роль успешно добавлена.";
            return RedirectToAction(nameof(RoleManagement));
        }

        public async Task<IActionResult> EditRole(int id)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var role = await apiClient.GetFromJsonAsync<Role>($"api/admin/roles/{id}");

            if (role == null)
                return NotFound();

            return View(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(Role role)
        {
            if (string.IsNullOrWhiteSpace(role.RoleName))
            {
                ModelState.AddModelError("", "�������� ���� �����������");
                return View(role);
            }

            if (role.RoleName.Length > 50)
            {
                ModelState.AddModelError("", "�������� ���� �� ������ ��������� 50 ��������");
                return View(role);
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(role.RoleName, @"^[A-Za-z�-��-���0-9\s]+$"))
            {
                ModelState.AddModelError("", "�������� ���� ������ ��������� ������ �����, ����� � �������");
                return View(role);
            }

            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.PutAsJsonAsync($"api/admin/roles/{role.RoleID}", role);
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            if (payload?.Success != true)
            {
                ModelState.AddModelError("", payload?.Message ?? "Не удалось обновить роль.");
                return View(role);
            }

            TempData["Success"] = "Роль успешно обновлена.";
            return RedirectToAction(nameof(RoleManagement));
        }

        public async Task<IActionResult> DeleteRole(int id)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var role = await apiClient.GetFromJsonAsync<Role>($"api/admin/roles/{id}");

            if (role == null)
                return NotFound();

            var usersCount = await apiClient.GetFromJsonAsync<int>($"api/admin/roles/{id}/users-count");

            ViewBag.UsersCount = usersCount;

            return View(role);
        }

        [HttpPost, ActionName("DeleteRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoleConfirmed(int id)
        {
            var apiClient = _httpClientFactory.CreateClient("LibraryApi");
            var response = await apiClient.DeleteAsync($"api/admin/roles/{id}");
            var payload = await response.Content.ReadFromJsonAsync<ApiCommandResponse>();
            if (payload?.Success == true)
            {
                TempData["Success"] = "Роль удалена.";
            }
            else
            {
                TempData["Error"] = payload?.Message ?? "Не удалось удалить роль.";
            }
            return RedirectToAction(nameof(RoleManagement));
        }

    }
}
