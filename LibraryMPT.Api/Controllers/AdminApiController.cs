using LibraryMPT.Data;
using LibraryMPT.Api.Extensions;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LibraryMPT.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminApiController : ControllerBase
{
    private readonly LibraryContext _context;

    public AdminApiController(LibraryContext context)
    {
        _context = context;
    }

    [HttpGet("security-dashboard")]
    public async Task<ActionResult<AdminSecurityDashboardResponse>> SecurityDashboard()
    {
        var result = new AdminSecurityDashboardResponse
        {
            TotalUsers = await _context.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Users").SingleAsync(),
            TotalBooks = await _context.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Books").SingleAsync(),
            DownloadsLast24h = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM BookLogs
                WHERE ActionType = 'DOWNLOAD' AND ActionAt >= DATEADD(DAY, -1, GETDATE())
            """).SingleAsync(),
            ReadsLast24h = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM BookLogs
                WHERE ActionType = 'READ' AND ActionAt >= DATEADD(DAY, -1, GETDATE())
            """).SingleAsync(),
            AuditEventsLast24h = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM AuditLog
                WHERE AuditDate >= DATEADD(DAY, -1, GETDATE())
            """).SingleAsync(),
            BlockedUsers = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM Users WHERE IsBlocked = 1
            """).SingleAsync(),
            TwoFactorUsers = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM Users WHERE IsTwoFactorEnabled = 1
            """).SingleAsync(),
            TwoFactorStudents = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value
                FROM Users u JOIN Roles r ON r.RoleID = u.RoleID
                WHERE u.IsTwoFactorEnabled = 1 AND r.RoleName = 'Student'
            """).SingleAsync(),
            ActiveSubscriptions = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value
                FROM Subscriptions
                WHERE FacultyID IS NOT NULL
                  AND (Status = 'Approved' OR Status IS NULL)
                  AND StartDate IS NOT NULL
                  AND EndDate IS NOT NULL
                  AND GETDATE() BETWEEN StartDate AND EndDate
            """).SingleAsync(),
            PendingSubscriptions = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM Subscriptions WHERE Status = 'Pending'
            """).SingleAsync(),
            BooksRequiringSubscription = await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM Books WHERE RequiresSubscription = 1
            """).SingleAsync(),
            DbSizeMb = await _context.Database.SqlQuery<int>($"""
                SELECT CAST(SUM(size) * 8.0 / 1024 AS INT) AS Value FROM sys.database_files
            """).SingleAsync()
        };

        result.LastAudit = await _context.AuditLog
            .FromSqlRaw("""
                SELECT TOP 10 * FROM AuditLog ORDER BY AuditDate DESC
            """)
            .AsNoTracking()
            .ToListAsync();

        result.AuditPopular = await _context.AuditSummaries
            .FromSqlRaw("""
                SELECT TOP 7 TableName, ActionType, COUNT(*) AS EventsCount
                FROM AuditLog
                WHERE AuditDate >= DATEADD(DAY, -1, GETDATE())
                GROUP BY TableName, ActionType
                ORDER BY COUNT(*) DESC
            """)
            .AsNoTracking()
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("user-management")]
    public async Task<ActionResult<AdminUserManagementResponse>> UserManagement(
        [FromQuery] string? search,
        [FromQuery] string? roleFilter,
        [FromQuery] string? facultyFilter,
        [FromQuery] string? statusFilter)
    {
        var sql = @"
            SELECT
                u.UserID,
                u.Email,
                u.RoleID,
                u.FacultyID,
                r.RoleName,
                f.FacultyName,
                u.IsBlocked
            FROM Users u
            JOIN Roles r ON r.RoleID = u.RoleID
            LEFT JOIN Faculty f ON f.FacultyID = u.FacultyID
            WHERE
                (@search IS NULL OR u.Email LIKE '%' + @search + '%')
                AND (@roleFilter IS NULL OR r.RoleName = @roleFilter)
                AND (@facultyFilter IS NULL OR f.FacultyName = @facultyFilter)
                AND (@statusFilter IS NULL OR
                    (@statusFilter = 'active' AND u.IsBlocked = 0) OR
                    (@statusFilter = 'blocked' AND u.IsBlocked = 1))
            ORDER BY u.UserID";

        var users = await _context.Set<UserAdminDto>()
            .FromSqlRaw(sql,
                new SqlParameter("@search", (object?)search ?? DBNull.Value),
                new SqlParameter("@roleFilter", (object?)roleFilter ?? DBNull.Value),
                new SqlParameter("@facultyFilter", (object?)facultyFilter ?? DBNull.Value),
                new SqlParameter("@statusFilter", (object?)statusFilter ?? DBNull.Value))
            .AsNoTracking()
            .ToListAsync();

        var roles = await _context.Roles.FromSqlRaw("SELECT * FROM Roles").AsNoTracking().ToListAsync();
        var faculties = await _context.Faculty.FromSqlRaw("SELECT * FROM Faculty").AsNoTracking().ToListAsync();

        return Ok(new AdminUserManagementResponse
        {
            Search = search,
            RoleFilter = roleFilter,
            FacultyFilter = facultyFilter,
            StatusFilter = statusFilter,
            Roles = roles,
            Faculties = faculties,
            Users = users
        });
    }

    [HttpGet("decrypt-last-name/{userId:int}")]
    public async Task<ActionResult<DecryptLastNameResponse>> DecryptLastName(int userId)
    {
        try
        {
            string? decryptedLastName;
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    BEGIN TRY
                        OPEN SYMMETRIC KEY LibraryKey
                        DECRYPTION BY CERTIFICATE LibraryCert;
                        
                        SELECT CONVERT(nvarchar(max), DecryptByKey(LastName)) AS DecryptedLastName
                        FROM Users
                        WHERE UserID = @userId;
                        
                        CLOSE SYMMETRIC KEY LibraryKey;
                    END TRY
                    BEGIN CATCH
                        SELECT CONVERT(nvarchar(max), LastName) AS DecryptedLastName
                        FROM Users
                        WHERE UserID = @userId;
                    END CATCH
                """;
                command.Parameters.Add(new SqlParameter("@userId", userId));

                var result = await command.ExecuteScalarAsync();
                decryptedLastName = result?.ToString();
            }
            finally
            {
                await connection.CloseAsync();
            }

            return Ok(new DecryptLastNameResponse
            {
                Success = true,
                LastName = decryptedLastName ?? "Не удалось расшифровать"
            });
        }
        catch (Exception ex)
        {
            return Ok(new DecryptLastNameResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    [HttpGet("role-assignment")]
    public async Task<ActionResult<AdminRoleAssignmentResponse>> RoleAssignment()
    {
        var users = await _context.Set<UserAdminDto>()
            .FromSqlRaw("""
                SELECT
                    u.UserID,
                    u.Email,
                    u.RoleID,
                    r.RoleName,
                    u.FacultyID,
                    f.FacultyName,
                    u.IsBlocked
                FROM Users u
                JOIN Roles r ON r.RoleID = u.RoleID
                LEFT JOIN Faculty f ON f.FacultyID = u.FacultyID
            """)
            .AsNoTracking()
            .ToListAsync();

        var roles = await _context.Roles.FromSqlRaw("SELECT * FROM Roles").AsNoTracking().ToListAsync();
        return Ok(new AdminRoleAssignmentResponse { Users = users, Roles = roles });
    }

    [HttpGet("audit-log")]
    public async Task<ActionResult<AdminAuditLogResponse>> AuditLog(
        [FromQuery] string? actionType,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir)
    {
        var normalizedSortBy = (sortBy ?? "date").Trim().ToLowerInvariant();
        var normalizedSortDir = (sortDir ?? "desc").Trim().ToLowerInvariant() == "asc" ? "ASC" : "DESC";
        var orderColumn = normalizedSortBy switch
        {
            "table" => "TableName",
            "action" => "ActionType",
            "user" => "UserName",
            _ => "AuditDate"
        };
        var orderClause = orderColumn == "AuditDate"
            ? $"AuditDate {normalizedSortDir}"
            : $"{orderColumn} {normalizedSortDir}, AuditDate DESC";

        var sql = $"""
            SELECT TOP 300 *
            FROM AuditLog
            WHERE (@action IS NULL OR ActionType = @action)
              AND (@search IS NULL
                   OR TableName LIKE '%' + @search + '%'
                   OR ActionType LIKE '%' + @search + '%'
                   OR UserName LIKE '%' + @search + '%'
                   OR OldData LIKE '%' + @search + '%'
                   OR NewData LIKE '%' + @search + '%')
            ORDER BY {orderClause}
        """;

        var logs = await _context.AuditLog
            .FromSqlRaw(sql,
                new SqlParameter("@action", (object?)actionType ?? DBNull.Value),
                new SqlParameter("@search", (object?)search ?? DBNull.Value))
            .AsNoTracking()
            .ToListAsync();

        return Ok(new AdminAuditLogResponse
        {
            ActionType = actionType,
            Search = search,
            SortBy = normalizedSortBy,
            SortDir = normalizedSortDir.ToLowerInvariant(),
            Logs = logs
        });
    }

    [HttpGet("faculties")]
    public async Task<ActionResult<List<Faculty>>> Faculties([FromQuery] string? search)
    {
        var sql = @"
            SELECT FacultyID, FacultyName
            FROM Faculty
            WHERE (@search IS NULL OR FacultyName LIKE '%' + @search + '%')
            ORDER BY FacultyName";
        var faculties = await _context.Faculty
            .FromSqlRaw(sql, new SqlParameter("@search", (object?)search ?? DBNull.Value))
            .AsNoTracking()
            .ToListAsync();
        return Ok(faculties);
    }

    [HttpGet("faculties/{id:int}")]
    public async Task<ActionResult<Faculty>> FacultyById(int id)
    {
        var faculty = await _context.Faculty
            .FromSqlRaw("SELECT * FROM Faculty WHERE FacultyID = @id", new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return faculty is null ? NotFound() : Ok(faculty);
    }

    [HttpPost("faculties")]
    public async Task<ActionResult<ApiCommandResponse>> AddFaculty([FromBody] Faculty faculty)
    {
        var exists = await _context.Faculty
            .FromSqlRaw("SELECT * FROM Faculty WHERE FacultyName = @name", new SqlParameter("@name", faculty.FacultyName.Trim()))
            .AnyAsync();
        if (exists)
        {
            return Ok(new ApiCommandResponse { Success = false, Message = "Факультет с таким названием уже существует." });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Faculty (FacultyName) VALUES (@name)",
            new SqlParameter("@name", faculty.FacultyName.Trim()));

        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("faculties/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> EditFaculty(int id, [FromBody] Faculty faculty)
    {
        if (id != faculty.FacultyID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Faculty id mismatch." });
        }

        var exists = await _context.Faculty
            .FromSqlRaw("SELECT * FROM Faculty WHERE FacultyName = @name AND FacultyID != @id",
                new SqlParameter("@name", faculty.FacultyName.Trim()),
                new SqlParameter("@id", faculty.FacultyID))
            .AnyAsync();
        if (exists)
        {
            return Ok(new ApiCommandResponse { Success = false, Message = "Факультет с таким названием уже существует." });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Faculty SET FacultyName = @name WHERE FacultyID = @id",
            new SqlParameter("@name", faculty.FacultyName.Trim()),
            new SqlParameter("@id", faculty.FacultyID));

        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("faculties/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> DeleteFaculty(int id)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM Faculty WHERE FacultyID = @id",
            new SqlParameter("@id", id));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<Role>>> Roles([FromQuery] string? search)
    {
        var sql = @"
            SELECT RoleID, RoleName
            FROM Roles
            WHERE (@search IS NULL OR RoleName LIKE '%' + @search + '%')
            ORDER BY RoleName";
        var roles = await _context.Roles
            .FromSqlRaw(sql, new SqlParameter("@search", (object?)search ?? DBNull.Value))
            .AsNoTracking()
            .ToListAsync();
        return Ok(roles);
    }

    [HttpGet("roles/{id:int}")]
    public async Task<ActionResult<Role>> RoleById(int id)
    {
        var role = await _context.Roles
            .FromSqlRaw("SELECT * FROM Roles WHERE RoleID = @id", new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost("roles")]
    public async Task<ActionResult<ApiCommandResponse>> AddRole([FromBody] Role role)
    {
        var exists = await _context.Roles
            .FromSqlRaw("SELECT * FROM Roles WHERE RoleName = @name", new SqlParameter("@name", role.RoleName.Trim()))
            .AnyAsync();
        if (exists)
        {
            return Ok(new ApiCommandResponse { Success = false, Message = "Роль с таким названием уже существует." });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Roles (RoleName) VALUES (@name)",
            new SqlParameter("@name", role.RoleName.Trim()));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPut("roles/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> EditRole(int id, [FromBody] Role role)
    {
        if (id != role.RoleID)
        {
            return BadRequest(new ApiCommandResponse { Success = false, Message = "Role id mismatch." });
        }

        var exists = await _context.Roles
            .FromSqlRaw("SELECT * FROM Roles WHERE RoleName = @name AND RoleID != @id",
                new SqlParameter("@name", role.RoleName.Trim()),
                new SqlParameter("@id", role.RoleID))
            .AnyAsync();
        if (exists)
        {
            return Ok(new ApiCommandResponse { Success = false, Message = "Роль с таким названием уже существует." });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Roles SET RoleName = @name WHERE RoleID = @id",
            new SqlParameter("@name", role.RoleName.Trim()),
            new SqlParameter("@id", role.RoleID));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("roles/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> DeleteRole(int id)
    {
        var usersCount = await _context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Users WHERE RoleID = {id}")
            .SingleAsync();
        if (usersCount > 0)
        {
            return Ok(new ApiCommandResponse
            {
                Success = false,
                Message = $"Невозможно удалить роль: используется у {usersCount} пользователей."
            });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM Roles WHERE RoleID = @id",
            new SqlParameter("@id", id));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("roles/{id:int}/users-count")]
    public async Task<ActionResult<int>> RoleUsersCount(int id)
    {
        var usersCount = await _context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Users WHERE RoleID = {id}")
            .SingleAsync();
        return Ok(usersCount);
    }

    [HttpPost("users/{id:int}/toggle-block")]
    public async Task<ActionResult<ApiCommandResponse>> ToggleBlock(int id)
    {
        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Users
            SET IsBlocked = CASE WHEN IsBlocked = 1 THEN 0 ELSE 1 END
            WHERE UserID = @id
        """, new SqlParameter("@id", id));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpDelete("users/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> DeleteUser(int id)
    {
        var user = await _context.Set<UserAdminDto>()
            .FromSqlRaw("""
                SELECT
                    u.UserID,
                    u.Email,
                    u.RoleID,
                    u.FacultyID,
                    r.RoleName,
                    f.FacultyName,
                    u.IsBlocked
                FROM Users u
                JOIN Roles r ON r.RoleID = u.RoleID
                LEFT JOIN Faculty f ON f.FacultyID = u.FacultyID
                WHERE u.UserID = @id
            """, new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new ApiCommandResponse { Success = false, Message = "Пользователь не найден." });
        }

        if (user.RoleName == "Admin")
        {
            return Ok(new ApiCommandResponse { Success = false, Message = "Нельзя удалить администратора." });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM Users WHERE UserID = @id",
            new SqlParameter("@id", id));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPost("users/update-role")]
    public async Task<ActionResult<ApiCommandResponse>> UpdateUserRole([FromBody] UpdateUserRoleRequest request)
    {
        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Users
            SET RoleID = @roleId
            WHERE UserID = @userId
        """,
            new SqlParameter("@roleId", request.RoleId),
            new SqlParameter("@userId", request.UserId));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPost("clear-audit-logs")]
    public async Task<ActionResult<ApiCommandResponse>> ClearAuditLogs()
    {
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM AuditLog");
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("backups")]
    public ActionResult<AdminBackupResponse> Backups()
    {
        var backupDir = GetSqlServerBackupPath();
        var backupFiles = new List<BackupFileDto>();
        if (Directory.Exists(backupDir))
        {
            foreach (var file in Directory.GetFiles(backupDir, "LibraryMPT_*.bak"))
            {
                var info = new FileInfo(file);
                backupFiles.Add(new BackupFileDto
                {
                    Name = info.Name,
                    Date = info.LastWriteTime,
                    Size = info.Length
                });
            }
        }

        return Ok(new AdminBackupResponse
        {
            BackupDir = backupDir,
            BackupFiles = backupFiles.OrderByDescending(x => x.Date).ToList()
        });
    }

    [HttpPost("backups/create")]
    public async Task<ActionResult<ApiCommandResponse>> CreateBackup()
    {
        try
        {
            var backupDir = GetSqlServerBackupPath();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupPath = Path.Combine(backupDir, $"LibraryMPT_Backup_{timestamp}.bak");
            var sql = $@"
BACKUP DATABASE [ElectronicLibraryv5]
TO DISK = N'{backupPath}'
WITH FORMAT, INIT,
NAME = N'LibraryMPT Full Backup {timestamp}',
SKIP, NOREWIND, NOUNLOAD, STATS = 10";
            await _context.Database.ExecuteSqlRawAsync(sql);
            return Ok(new ApiCommandResponse
            {
                Success = true,
                Message = $"Резервная копия создана: LibraryMPT_Backup_{timestamp}.bak"
            });
        }
        catch (Exception ex)
        {
            return Ok(new ApiCommandResponse { Success = false, Message = ex.Message });
        }
    }

    [HttpGet("backups/download")]
    public IActionResult DownloadBackup([FromQuery] string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return NotFound();
        var filePath = Path.Combine(GetSqlServerBackupPath(), fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound();
        return PhysicalFile(filePath, "application/octet-stream", fileName);
    }

    [HttpDelete("backups")]
    public ActionResult<ApiCommandResponse> DeleteBackup([FromQuery] string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Ok(new ApiCommandResponse { Success = false, Message = "Имя файла не указано" });

        var filePath = Path.Combine(GetSqlServerBackupPath(), fileName);
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
            return Ok(new ApiCommandResponse { Success = true, Message = $"Файл {fileName} удален" });
        }
        return Ok(new ApiCommandResponse { Success = false, Message = "Файл не найден" });
    }

    [HttpPost("backups/restore")]
    public async Task<ActionResult<ApiCommandResponse>> RestoreBackup([FromQuery] string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Ok(new ApiCommandResponse { Success = false, Message = "Имя файла не указано" });

        var filePath = Path.Combine(GetSqlServerBackupPath(), fileName);
        if (!System.IO.File.Exists(filePath))
            return Ok(new ApiCommandResponse { Success = false, Message = "Файл бэкапа не найден" });

        try
        {
            var masterConnectionString = _context.Database.GetConnectionString()!.Replace("ElectronicLibraryv5", "master");
            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync();
            await using (var cmd = new SqlCommand("ALTER DATABASE [ElectronicLibraryv5] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = new SqlCommand($@"RESTORE DATABASE [ElectronicLibraryv5] FROM DISK = N'{filePath}' WITH REPLACE;", connection))
            {
                cmd.CommandTimeout = 300;
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = new SqlCommand("ALTER DATABASE [ElectronicLibraryv5] SET MULTI_USER;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
            return Ok(new ApiCommandResponse { Success = true, Message = $"База восстановлена из {fileName}" });
        }
        catch (Exception ex)
        {
            return Ok(new ApiCommandResponse { Success = false, Message = ex.Message });
        }
    }

    [HttpGet("create-user-lookups")]
    public async Task<ActionResult<CreateUserLookupsResponse>> CreateUserLookups()
    {
        var roles = await _context.Roles.FromSqlRaw("SELECT * FROM Roles").AsNoTracking().ToListAsync();
        var faculties = await _context.Faculty.FromSqlRaw("SELECT * FROM Faculty").AsNoTracking().ToListAsync();
        return Ok(new CreateUserLookupsResponse { Roles = roles, Faculties = faculties });
    }

    [HttpPost("users")]
    public async Task<ActionResult<ApiCommandResponse>> CreateUser([FromBody] User user)
    {
        await _context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO Users (Email, PasswordHash, RoleID, FacultyID)
            VALUES (@email, @password, @roleId, @facultyId)",
            new SqlParameter("@email", user.Email),
            new SqlParameter("@password", user.PasswordHash),
            new SqlParameter("@roleId", user.RoleID),
            new SqlParameter("@facultyId", (object?)user.FacultyID ?? DBNull.Value));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpGet("users/{id:int}/edit")]
    public async Task<ActionResult<EditUserViewResponse>> EditUserData(int id)
    {
        var currentUserId = User.GetUserId();
        var user = await _context.Set<UserAdminDto>()
            .FromSqlRaw("""
                SELECT
                    u.UserID, u.Email, u.RoleID, r.RoleName, u.FacultyID, f.FacultyName, u.IsBlocked
                FROM Users u
                JOIN Roles r ON r.RoleID = u.RoleID
                LEFT JOIN Faculty f ON f.FacultyID = u.FacultyID
                WHERE u.UserID = @id
            """, new SqlParameter("@id", id))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (user == null)
            return NotFound();

        var decrypted = await TryDecryptLastNameAsync(id);
        var roles = await _context.Roles.FromSqlRaw("SELECT * FROM Roles").AsNoTracking().ToListAsync();
        var faculties = await _context.Faculty.FromSqlRaw("SELECT * FROM Faculty").AsNoTracking().ToListAsync();
        var isEditingSelf = currentUserId == id;
        var isAdminOrLibrarian = user.RoleName == "Admin" || user.RoleName == "Librarian";
        var canEditFaculty = !(isEditingSelf && isAdminOrLibrarian) && user.RoleName != "Admin" && user.RoleName != "Librarian";

        return Ok(new EditUserViewResponse
        {
            User = user,
            DecryptedLastName = decrypted ?? "Не удалось расшифровать",
            Roles = roles,
            Faculties = faculties,
            CanEditFaculty = canEditFaculty
        });
    }

    [HttpPut("users/{id:int}")]
    public async Task<ActionResult<ApiCommandResponse>> EditUser(int id, [FromBody] UpdateUserRequest request)
    {
        var dto = request.Dto;
        if (id != dto.UserID)
            return BadRequest(new ApiCommandResponse { Success = false, Message = "User id mismatch." });

        var currentUser = await _context.Set<UserAdminDto>()
            .FromSqlRaw("""
                SELECT
                    u.UserID, u.Email, u.RoleID, r.RoleName, u.FacultyID, f.FacultyName, u.IsBlocked
                FROM Users u
                JOIN Roles r ON r.RoleID = u.RoleID
                LEFT JOIN Faculty f ON f.FacultyID = u.FacultyID
                WHERE u.UserID = @id
            """, new SqlParameter("@id", dto.UserID))
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (currentUser == null)
            return Ok(new ApiCommandResponse { Success = false, Message = "Пользователь не найден." });

        var isEditingSelf = User.GetUserId() == dto.UserID;
        var isAdminOrLibrarian = currentUser.RoleName == "Admin" || currentUser.RoleName == "Librarian";

        if (isEditingSelf && isAdminOrLibrarian)
            dto.FacultyID = null;

        if (string.IsNullOrWhiteSpace(dto.Email) || !Regex.IsMatch(dto.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return Ok(new ApiCommandResponse { Success = false, Message = "Некорректный email." });
        if (dto.RoleID <= 0)
            return Ok(new ApiCommandResponse { Success = false, Message = "Выберите роль." });

        string updateSql;
        var parameters = new List<SqlParameter>
        {
            new("@em", dto.Email.Trim()),
            new("@roleId", dto.RoleID),
            new("@id", dto.UserID)
        };

        if (isEditingSelf && isAdminOrLibrarian)
        {
            updateSql = """
                UPDATE Users
                SET Email = @em, RoleID = @roleId, FacultyID = NULL
                WHERE UserID = @id
            """;
        }
        else if (dto.FacultyID.HasValue && dto.FacultyID.Value > 0 && currentUser.RoleName != "Admin" && currentUser.RoleName != "Librarian")
        {
            updateSql = """
                UPDATE Users
                SET Email = @em, RoleID = @roleId, FacultyID = @facultyId
                WHERE UserID = @id
            """;
            parameters.Add(new SqlParameter("@facultyId", dto.FacultyID.Value));
        }
        else
        {
            updateSql = """
                UPDATE Users
                SET Email = @em, RoleID = @roleId, FacultyID = NULL
                WHERE UserID = @id
            """;
        }

        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(updateSql, parameters.ToArray());
        return Ok(new ApiCommandResponse
        {
            Success = rowsAffected > 0,
            Message = rowsAffected > 0 ? $"Пользователь обновлен. Изменено строк: {rowsAffected}" : "Не удалось обновить пользователя."
        });
    }

    [HttpGet("create-staff-lookups")]
    public async Task<ActionResult<CreateUserLookupsResponse>> CreateStaffLookups()
    {
        var roles = await _context.Roles
            .FromSqlRaw("""
                SELECT *
                FROM Roles
                WHERE RoleName IN ('Admin', 'Librarian', 'InstitutionRepresentative')
            """)
            .AsNoTracking()
            .ToListAsync();
        var faculties = await _context.Faculty.FromSqlRaw("SELECT * FROM Faculty").AsNoTracking().ToListAsync();
        return Ok(new CreateUserLookupsResponse { Roles = roles, Faculties = faculties });
    }

    [HttpPost("staff")]
    public async Task<ActionResult<CreateStaffResult>> CreateStaff([FromBody] CreateStaffRequest request)
    {
        var dto = request.Dto;
        var selectedRole = await _context.Roles
            .FromSqlRaw("SELECT * FROM Roles WHERE RoleID = @roleId", new SqlParameter("@roleId", dto.RoleID))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (selectedRole != null && selectedRole.RoleName == "InstitutionRepresentative" && !dto.FacultyID.HasValue)
            return Ok(new CreateStaffResult { Success = false, Message = "Для представителя учреждения нужен факультет." });

        if (selectedRole != null && (selectedRole.RoleName == "Admin" || selectedRole.RoleName == "Librarian"))
            dto.FacultyID = null;

        var generatedPassword = GenerateSecurePassword();
        CreatePasswordHash(generatedPassword, out var hash, out var salt);

        await _context.Database.ExecuteSqlRawAsync("""
            BEGIN TRY
                OPEN SYMMETRIC KEY LibraryKey
                DECRYPTION BY CERTIFICATE LibraryCert;

                INSERT INTO Users
                (Username, PasswordHash, PasswordSalt, FirstName, LastName, Email, RoleID, FacultyID)
                VALUES
                (
                    @un, @ph, @ps, @fn,
                    EncryptByKey(Key_GUID('LibraryKey'), CONVERT(nvarchar(max), @ln)),
                    @em, @role, @facultyId
                );

                CLOSE SYMMETRIC KEY LibraryKey;
            END TRY
            BEGIN CATCH
                INSERT INTO Users
                (Username, PasswordHash, PasswordSalt, FirstName, LastName, Email, RoleID, FacultyID)
                VALUES
                (
                    @un, @ph, @ps, @fn,
                    CONVERT(varbinary(max), @ln),
                    @em, @role, @facultyId
                );
            END CATCH
        """,
            new SqlParameter("@un", dto.Username),
            new SqlParameter("@ph", hash),
            new SqlParameter("@ps", salt),
            new SqlParameter("@fn", dto.FirstName),
            new SqlParameter("@ln", dto.LastName),
            new SqlParameter("@em", dto.Email),
            new SqlParameter("@role", dto.RoleID),
            new SqlParameter("@facultyId", (object?)dto.FacultyID ?? DBNull.Value));

        return Ok(new CreateStaffResult
        {
            Success = true,
            GeneratedPassword = generatedPassword,
            RoleName = selectedRole?.RoleName ?? "Сотрудник"
        });
    }

    private async Task<string?> TryDecryptLastNameAsync(int id)
    {
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                BEGIN TRY
                    OPEN SYMMETRIC KEY LibraryKey
                    DECRYPTION BY CERTIFICATE LibraryCert;
                    
                    SELECT CONVERT(nvarchar(max), DecryptByKey(LastName)) AS DecryptedLastName
                    FROM Users
                    WHERE UserID = @id;
                    
                    CLOSE SYMMETRIC KEY LibraryKey;
                END TRY
                BEGIN CATCH
                    SELECT CONVERT(nvarchar(max), LastName) AS DecryptedLastName
                    FROM Users
                    WHERE UserID = @id;
                END CATCH
            """;
            command.Parameters.Add(new SqlParameter("@id", id));
            var result = await command.ExecuteScalarAsync();
            return result?.ToString();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static string GetSqlServerBackupPath()
    {
        var paths = new[]
        {
            @"C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup",
            @"C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup",
            @"C:\Program Files\Microsoft SQL Server\MSSQL14.SQLEXPRESS\MSSQL\Backup",
            @"C:\Program Files\Microsoft SQL Server\MSSQL13.SQLEXPRESS\MSSQL\Backup",
            @"C:\SQLBackups"
        };
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                return path;
        }

        var fallback = @"C:\SQLBackups";
        if (!Directory.Exists(fallback))
            Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string GenerateSecurePassword()
    {
        const string uppercase = "ABCDEFGHJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%^&*";
        const string allChars = uppercase + lowercase + digits + special;

        var password = new StringBuilder();
        var random = new Random();
        password.Append(uppercase[random.Next(uppercase.Length)]);
        password.Append(lowercase[random.Next(lowercase.Length)]);
        password.Append(digits[random.Next(digits.Length)]);
        password.Append(special[random.Next(special.Length)]);
        for (var i = password.Length; i < 16; i++)
            password.Append(allChars[random.Next(allChars.Length)]);

        var shuffled = password.ToString().ToCharArray();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return new string(shuffled);
    }

    private static byte[] GenerateSalt()
    {
        var salt = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }

    private static void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
    {
        var saltBytes = GenerateSalt();
        passwordSalt = Convert.ToBase64String(saltBytes);

        using var sha = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(password + passwordSalt);
        passwordHash = Convert.ToBase64String(sha.ComputeHash(bytes));
    }
}

