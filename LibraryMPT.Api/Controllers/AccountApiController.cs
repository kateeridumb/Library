using LibraryMPT.Data;
using LibraryMPT.Api.Extensions;
using LibraryMPT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LibraryMPT.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/account")]
public sealed class AccountApiController : ControllerBase
{
    private readonly LibraryContext _context;
    private readonly IConfiguration _configuration;

    public AccountApiController(LibraryContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<Role>>> GetRoles()
    {
        var roles = await _context.Roles.FromSqlRaw("SELECT * FROM Roles").AsNoTracking().ToListAsync();
        return Ok(roles);
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResult>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var userId = await _context.Database
            .SqlQuery<int?>($"SELECT UserID AS Value FROM Users WHERE Email = {request.Email ?? string.Empty}")
            .SingleOrDefaultAsync();

        if (!userId.HasValue)
        {
            return Ok(new ForgotPasswordResult { UserExists = false });
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
        var expiryDate = DateTime.UtcNow.AddHours(24);

        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Users
            SET PasswordResetToken = @token,
                PasswordResetTokenExpiry = @expiry
            WHERE UserID = @userId
        """,
            new SqlParameter("@token", token),
            new SqlParameter("@expiry", expiryDate),
            new SqlParameter("@userId", userId.Value));

        return Ok(new ForgotPasswordResult
        {
            UserExists = true,
            UserId = userId.Value,
            Token = token
        });
    }

    [HttpGet("validate-reset-token")]
    public async Task<ActionResult<ApiCommandResponse>> ValidateResetToken([FromQuery] string token)
    {
        var userId = await _context.Database
            .SqlQuery<int?>($"SELECT UserID AS Value FROM Users WHERE PasswordResetToken = {token ?? string.Empty} AND PasswordResetTokenExpiry > GETUTCDATE()")
            .SingleOrDefaultAsync();
        return Ok(new ApiCommandResponse { Success = userId.HasValue });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiCommandResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var userId = await _context.Database
            .SqlQuery<int?>($"SELECT UserID AS Value FROM Users WHERE PasswordResetToken = {request.Token ?? string.Empty} AND PasswordResetTokenExpiry > GETUTCDATE()")
            .SingleOrDefaultAsync();

        if (!userId.HasValue)
        {
            return Ok(new ApiCommandResponse { Success = false, Message = "Ссылка недействительна или устарела." });
        }

        CreatePasswordHash(request.Password, out var hash, out var salt);

        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Users
            SET PasswordHash = @hash,
                PasswordSalt = @salt,
                PasswordResetToken = NULL,
                PasswordResetTokenExpiry = NULL
            WHERE UserID = @userId
        """,
            new SqlParameter("@hash", hash),
            new SqlParameter("@salt", salt),
            new SqlParameter("@userId", userId.Value));

        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiCommandResponse>> Register([FromBody] AccountRegisterRequest request)
    {
        var roles = await _context.Roles.FromSqlRaw("SELECT * FROM Roles").AsNoTracking().ToListAsync();
        var defaultRole = roles.FirstOrDefault(r => r.RoleName == "Student");
        if (defaultRole == null)
        {
            return Ok(new ApiCommandResponse { Success = false, Message = "Не найдена роль студента для регистрации." });
        }

        CreatePasswordHash(request.Password, out var hash, out var salt);

        await _context.Database.ExecuteSqlRawAsync("""
            BEGIN TRY
                OPEN SYMMETRIC KEY LibraryKey
                DECRYPTION BY CERTIFICATE LibraryCert;

                INSERT INTO Users
                (Username, PasswordHash, PasswordSalt, FirstName, LastName, Email, RoleID)
                VALUES
                (
                    @un, @ph, @ps, @fn,
                    EncryptByKey(Key_GUID('LibraryKey'), CONVERT(nvarchar(max), @ln)),
                    @em, @roleId
                );

                CLOSE SYMMETRIC KEY LibraryKey;
            END TRY
            BEGIN CATCH
                BEGIN TRY
                    CLOSE SYMMETRIC KEY LibraryKey;
                END TRY
                BEGIN CATCH
                END CATCH

                INSERT INTO Users
                (Username, PasswordHash, PasswordSalt, FirstName, LastName, Email, RoleID)
                VALUES
                (
                    @un, @ph, @ps, @fn,
                    CONVERT(varbinary(max), @ln),
                    @em, @roleId
                );
            END CATCH
        """,
            new SqlParameter("@un", request.Username),
            new SqlParameter("@ph", hash),
            new SqlParameter("@ps", salt),
            new SqlParameter("@fn", request.FirstName),
            new SqlParameter("@ln", request.LastName),
            new SqlParameter("@em", request.Email),
            new SqlParameter("@roleId", defaultRole.RoleID));

        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AccountLoginResult>> Login([FromBody] AccountLoginRequest request)
    {
        var user = await _context.LoginUsers
            .FromSqlRaw("""
                SELECT
                    u.UserID,
                    u.Username,
                    u.PasswordHash,
                    u.PasswordSalt,
                    u.RoleID,
                    r.RoleName,
                    u.IsBlocked,
                    u.IsTwoFactorEnabled,
                    u.Email,
                    u.FirstName
                FROM Users u
                JOIN Roles r ON r.RoleID = u.RoleID
                WHERE u.Username = @username
            """, new SqlParameter("@username", request.Username))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (user == null || user.IsBlocked)
        {
            return Ok(new AccountLoginResult { Success = false, Error = "Неверный логин или пароль" });
        }

        var hash = HashPassword(request.Password, user.PasswordSalt);
        if (hash != user.PasswordHash)
        {
            return Ok(new AccountLoginResult { Success = false, Error = "Неверный логин или пароль" });
        }

        return Ok(new AccountLoginResult
        {
            Success = true,
            RequiresTwoFactor = user.IsTwoFactorEnabled && user.RoleName == "Student",
            UserId = user.UserID,
            TwoFactorToken = user.IsTwoFactorEnabled && user.RoleName == "Student"
                ? CreateTwoFactorToken(user.UserID, user.Username, user.RoleName)
                : null,
            Username = user.Username,
            RoleName = user.RoleName,
            Email = user.Email,
            FirstName = user.FirstName
        });
    }

    [HttpPost("set-twofactor-code")]
    public async Task<ActionResult<ApiCommandResponse>> SetTwoFactorCode([FromBody] SetTwoFactorCodeRequest request)
    {
        if (!TryGetUserIdFromTwoFactorToken(request.TwoFactorToken, out var userId))
        {
            return Ok(new ApiCommandResponse { Success = false, Message = "Недействительный 2FA токен." });
        }

        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Users
            SET TwoFactorCode = @code,
                TwoFactorCodeExpiry = @expiry
            WHERE UserID = @userId
        """,
            new SqlParameter("@code", request.Code),
            new SqlParameter("@expiry", request.ExpiryUtc),
            new SqlParameter("@userId", userId));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPost("verify-twofactor-code")]
    public async Task<ActionResult<ApiCommandResponse>> VerifyTwoFactorCode([FromBody] TwoFactorCodeRequest request)
    {
        if (!TryGetUserIdFromTwoFactorToken(request.TwoFactorToken, out var userId))
        {
            return Ok(new ApiCommandResponse { Success = false });
        }

        var user = await _context.Database
            .SqlQuery<int?>($"""
                SELECT UserID AS Value
                FROM Users
                WHERE UserID = {userId}
                    AND TwoFactorCode = {request.Code ?? string.Empty}
                    AND TwoFactorCodeExpiry > GETUTCDATE()
                    AND IsTwoFactorEnabled = 1
            """)
            .SingleOrDefaultAsync();

        return Ok(new ApiCommandResponse { Success = user.HasValue });
    }

    [HttpPost("clear-twofactor-code")]
    public async Task<ActionResult<ApiCommandResponse>> ClearTwoFactorCode([FromBody] TwoFactorCodeRequest request)
    {
        if (!TryGetUserIdFromTwoFactorToken(request.TwoFactorToken, out var userId))
        {
            return Ok(new ApiCommandResponse { Success = false, Message = "Недействительный 2FA токен." });
        }

        await _context.Database.ExecuteSqlRawAsync("""
            UPDATE Users
            SET TwoFactorCode = NULL,
                TwoFactorCodeExpiry = NULL
            WHERE UserID = @userId
        """, new SqlParameter("@userId", userId));
        return Ok(new ApiCommandResponse { Success = true });
    }

    [HttpPost("guest-login")]
    public async Task<ActionResult<GuestLoginResult>> GuestLogin()
    {
        const string guestUsername = "guest";
        var guestUserId = await _context.Database
            .SqlQuery<int?>($"SELECT UserID AS Value FROM Users WHERE Username = {guestUsername}")
            .SingleOrDefaultAsync();

        if (!guestUserId.HasValue)
        {
            const string studentRole = "Student";
            var roleId = await _context.Database
                .SqlQuery<int?>($"SELECT RoleID AS Value FROM Roles WHERE RoleName = {studentRole}")
                .SingleOrDefaultAsync();

            if (!roleId.HasValue)
            {
                roleId = await _context.Database.SqlQuery<int?>($"SELECT TOP 1 RoleID AS Value FROM Roles").SingleOrDefaultAsync();
            }
            if (!roleId.HasValue)
            {
                return Ok(new GuestLoginResult { Success = false });
            }

            CreatePasswordHash(Guid.NewGuid().ToString("N"), out var hash, out var salt);

            await _context.Database.ExecuteSqlRawAsync("""
                BEGIN TRY
                    OPEN SYMMETRIC KEY LibraryKey
                    DECRYPTION BY CERTIFICATE LibraryCert;

                    INSERT INTO Users
                    (Username, PasswordHash, PasswordSalt, FirstName, LastName, Email, RoleID)
                    VALUES
                    (
                        @un, @ph, @ps, @fn,
                        EncryptByKey(Key_GUID('LibraryKey'), CONVERT(nvarchar(max), @ln)),
                        @em, @roleId
                    );

                    CLOSE SYMMETRIC KEY LibraryKey;
                END TRY
                BEGIN CATCH
                    BEGIN TRY
                        CLOSE SYMMETRIC KEY LibraryKey;
                    END TRY
                    BEGIN CATCH
                    END CATCH

                    INSERT INTO Users
                    (Username, PasswordHash, PasswordSalt, FirstName, LastName, Email, RoleID)
                    VALUES
                    (
                        @un, @ph, @ps, @fn,
                        CONVERT(varbinary(max), @ln),
                        @em, @roleId
                    );
                END CATCH
            """,
                new SqlParameter("@un", guestUsername),
                new SqlParameter("@ph", hash),
                new SqlParameter("@ps", salt),
                new SqlParameter("@fn", "Гость"),
                new SqlParameter("@ln", "Гость"),
                new SqlParameter("@em", "guest@local"),
                new SqlParameter("@roleId", roleId.Value));

            guestUserId = await _context.Database
                .SqlQuery<int?>($"SELECT UserID AS Value FROM Users WHERE Username = {guestUsername}")
                .SingleOrDefaultAsync();
        }

        return Ok(new GuestLoginResult
        {
            Success = guestUserId.HasValue,
            UserId = guestUserId ?? 0
        });
    }

    [HttpPost("toggle-twofactor")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<ApiCommandResponse>> ToggleTwoFactor([FromBody] ToggleTwoFactorRequest request)
    {
        var userId = User.GetUserId();
        if (userId == 0)
        {
            return Unauthorized(new ApiCommandResponse { Success = false, Message = "Unauthorized" });
        }

        if (request.Enabled)
        {
            var userEmail = await _context.Database
                .SqlQuery<string>($"SELECT Email AS Value FROM Users WHERE UserID = {userId}")
                .SingleOrDefaultAsync();

            if (string.IsNullOrEmpty(userEmail) || !userEmail.EndsWith("@gmail.com"))
            {
                return Ok(new ApiCommandResponse
                {
                    Success = false,
                    Message = "Для использования двухфакторной аутентификации необходим email Gmail"
                });
            }
        }

        if (request.Enabled)
        {
            await _context.Database.ExecuteSqlRawAsync("""
                UPDATE Users
                SET IsTwoFactorEnabled = 1
                WHERE UserID = @userId
            """, new SqlParameter("@userId", userId));
        }
        else
        {
            await _context.Database.ExecuteSqlRawAsync("""
                UPDATE Users
                SET IsTwoFactorEnabled = 0,
                    TwoFactorCode = NULL,
                    TwoFactorCodeExpiry = NULL
                WHERE UserID = @userId
            """, new SqlParameter("@userId", userId));
        }

        return Ok(new ApiCommandResponse { Success = true });
    }

    private static byte[] GenerateSalt()
    {
        var salt = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }

    private static string HashPassword(string password, string salt)
    {
        using var sha = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(password + salt);
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    private static void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
    {
        var saltBytes = GenerateSalt();
        passwordSalt = Convert.ToBase64String(saltBytes);

        using var sha = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(password + passwordSalt);
        passwordHash = Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    private string CreateTwoFactorToken(int userId, string username, string role)
    {
        var issuer = _configuration["JwtSettings:Issuer"] ?? "LibraryMPT";
        var audience = _configuration["JwtSettings:Audience"] ?? "LibraryMPT.Api";
        var key = _configuration["JwtSettings:Key"] ?? throw new InvalidOperationException("JwtSettings:Key is missing.");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role),
            new("purpose", "2fa")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool TryGetUserIdFromTwoFactorToken(string token, out int userId)
    {
        userId = 0;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var issuer = _configuration["JwtSettings:Issuer"] ?? "LibraryMPT";
        var audience = _configuration["JwtSettings:Audience"] ?? "LibraryMPT.Api";
        var key = _configuration["JwtSettings:Key"] ?? throw new InvalidOperationException("JwtSettings:Key is missing.");
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            var purpose = principal.FindFirst("purpose")?.Value;
            if (purpose != "2fa")
                return false;

            var rawUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(rawUserId, out userId) && userId > 0;
        }
        catch
        {
            return false;
        }
    }
}

