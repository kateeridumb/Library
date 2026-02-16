namespace LibraryMPT.Models;

public sealed class AccountLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class AccountLoginResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public int UserId { get; set; }
    public string? TwoFactorToken { get; set; }
    public string Username { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
}

public sealed class AccountRegisterRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int? RoleId { get; set; }
}

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class ForgotPasswordResult
{
    public bool UserExists { get; set; }
    public int? UserId { get; set; }
    public string? Token { get; set; }
}

public sealed class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class TwoFactorCodeRequest
{
    public string TwoFactorToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public sealed class SetTwoFactorCodeRequest
{
    public string TwoFactorToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiryUtc { get; set; }
}

public sealed class ToggleTwoFactorRequest
{
    public int UserId { get; set; }
    public bool Enabled { get; set; }
}

public sealed class GuestLoginResult
{
    public bool Success { get; set; }
    public int UserId { get; set; }
}

