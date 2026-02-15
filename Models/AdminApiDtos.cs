namespace LibraryMPT.Models;

public sealed class AdminSecurityDashboardResponse
{
    public int TotalUsers { get; set; }
    public int TotalBooks { get; set; }
    public int DownloadsLast24h { get; set; }
    public int ReadsLast24h { get; set; }
    public int AuditEventsLast24h { get; set; }
    public int BlockedUsers { get; set; }
    public int TwoFactorUsers { get; set; }
    public int TwoFactorStudents { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int PendingSubscriptions { get; set; }
    public int BooksRequiringSubscription { get; set; }
    public int DbSizeMb { get; set; }
    public List<AuditLog> LastAudit { get; set; } = new();
    public List<AuditSummaryDto> AuditPopular { get; set; } = new();
}

public sealed class AdminUserManagementResponse
{
    public string? Search { get; set; }
    public string? RoleFilter { get; set; }
    public string? FacultyFilter { get; set; }
    public string? StatusFilter { get; set; }
    public List<Role> Roles { get; set; } = new();
    public List<Faculty> Faculties { get; set; } = new();
    public List<UserAdminDto> Users { get; set; } = new();
}

public sealed class AdminRoleAssignmentResponse
{
    public List<UserAdminDto> Users { get; set; } = new();
    public List<Role> Roles { get; set; } = new();
}

public sealed class AdminAuditLogResponse
{
    public string? ActionType { get; set; }
    public string? Search { get; set; }
    public string SortBy { get; set; } = "date";
    public string SortDir { get; set; } = "desc";
    public List<AuditLog> Logs { get; set; } = new();
}

public sealed class DecryptLastNameResponse
{
    public bool Success { get; set; }
    public string? LastName { get; set; }
    public string? Error { get; set; }
}

public sealed class UpdateUserRoleRequest
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
}

public sealed class BackupFileDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public long Size { get; set; }
}

public sealed class AdminBackupResponse
{
    public string BackupDir { get; set; } = string.Empty;
    public List<BackupFileDto> BackupFiles { get; set; } = new();
}

public sealed class CreateUserLookupsResponse
{
    public List<Role> Roles { get; set; } = new();
    public List<Faculty> Faculties { get; set; } = new();
}

public sealed class EditUserViewResponse
{
    public UserAdminDto? User { get; set; }
    public string DecryptedLastName { get; set; } = string.Empty;
    public List<Role> Roles { get; set; } = new();
    public List<Faculty> Faculties { get; set; } = new();
    public bool CanEditFaculty { get; set; }
}

public sealed class UpdateUserRequest
{
    public UserAdminDto Dto { get; set; } = new();
    public int CurrentUserId { get; set; }
}

public sealed class CreateStaffRequest
{
    public StaffCreateDto Dto { get; set; } = new();
}

public sealed class CreateStaffResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? GeneratedPassword { get; set; }
    public string? RoleName { get; set; }
}

