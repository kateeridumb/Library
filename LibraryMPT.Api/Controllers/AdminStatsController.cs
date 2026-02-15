using LibraryMPT.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LibraryMPT.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/stats")]
public sealed class AdminStatsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AdminStatsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<AdminDashboardStatsDto>> GetDashboardStats()
    {
        var connectionString = _configuration.GetConnectionString("LibraryDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("Connection string 'LibraryDb' is not configured.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var dto = new AdminDashboardStatsDto
        {
            TotalUsers = await ExecuteIntScalarAsync(connection, "SELECT COUNT(*) FROM Users"),
            AdminCount = await ExecuteIntScalarAsync(connection, """
                SELECT COUNT(*)
                FROM Users u
                JOIN Roles r ON r.RoleID = u.RoleID
                WHERE r.RoleName = 'Admin'
                """),
            LibrarianCount = await ExecuteIntScalarAsync(connection, """
                SELECT COUNT(*)
                FROM Users u
                JOIN Roles r ON r.RoleID = u.RoleID
                WHERE r.RoleName = 'Librarian'
                """),
            ReaderCount = await ExecuteIntScalarAsync(connection, """
                SELECT COUNT(*)
                FROM Users u
                JOIN Roles r ON r.RoleID = u.RoleID
                WHERE r.RoleName = 'Student'
                """)
        };

        return Ok(dto);
    }

    private static async Task<int> ExecuteIntScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }
}

