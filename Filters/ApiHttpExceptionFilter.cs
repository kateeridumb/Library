using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LibraryMPT.Filters;

public sealed class ApiHttpExceptionFilter : IAsyncExceptionFilter
{
    private readonly ILogger<ApiHttpExceptionFilter> _logger;

    public ApiHttpExceptionFilter(ILogger<ApiHttpExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not HttpRequestException httpEx)
        {
            return;
        }

        if (httpEx.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(httpEx, "API returned unauthorized/forbidden. Redirecting to login.");
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Result = new RedirectToActionResult("Login", "Account", null);
            context.ExceptionHandled = true;
            return;
        }

        if (httpEx.StatusCode.HasValue && (int)httpEx.StatusCode.Value >= 500)
        {
            _logger.LogError(httpEx, "API returned server error {StatusCode}. Redirecting to error page.", (int)httpEx.StatusCode.Value);
            context.Result = new RedirectToActionResult("Error", "Home", null);
            context.ExceptionHandled = true;
        }
    }
}

