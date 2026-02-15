namespace LibraryMPT.Middleware
{



    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);

                if (context.Response.StatusCode >= 400)
                {
                    await HandleErrorResponse(context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Необработанное исключение");
                await HandleException(context, ex);
            }
        }

        private async Task HandleException(HttpContext context, Exception exception)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "text/html; charset=utf-8";

            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            var stackTrace = isDevelopment ? exception.StackTrace : null;

            var errorMessage = exception switch
            {
                InvalidOperationException => "Внутренняя ошибка приложения",
                UnauthorizedAccessException => "У вас нет прав доступа",
                ArgumentException ex => $"Неверный параметр: {ex.Message}",
                _ => "Произошла неиспользованная ошибка. Пожалуйста, повторите попытку позже."
            };

            var html = GenerateErrorHtml(
                500,
                "Внутренняя ошибка сервера",
                errorMessage,
                stackTrace
            );

            await context.Response.WriteAsync(html);
        }

        private async Task HandleErrorResponse(HttpContext context)
        {
            if (!context.Response.HasStarted)
            {
                var statusCode = context.Response.StatusCode;
                var title = GetErrorTitle(statusCode);
                var message = GetErrorMessage(statusCode);

                context.Response.ContentType = "text/html; charset=utf-8";
                var html = GenerateErrorHtml(statusCode, title, message, null);

                await context.Response.WriteAsync(html);
            }
        }

        private string GenerateErrorHtml(int statusCode, string title, string message, string? stackTrace)
        {
            var stackTraceHtml = string.IsNullOrEmpty(stackTrace) 
                ? "" 
                : $@"
                    <details class='error-details'>
                        <summary style='cursor: pointer; color: #667eea; font-weight: 600;'>📋 Детали ошибки</summary>
                        <pre style='margin-top: 10px; white-space: pre-wrap; word-wrap: break-word;'>{System.Net.WebUtility.HtmlEncode(stackTrace)}</pre>
                    </details>";

            return $@"
<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Ошибка {statusCode}</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            padding: 20px;
        }}

        .error-container {{
            background: white;
            border-radius: 15px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            padding: 60px 40px;
            max-width: 600px;
            width: 100%;
            text-align: center;
            animation: slideUp 0.5s ease-out;
        }}

        @keyframes slideUp {{
            from {{
                opacity: 0;
                transform: translateY(30px);
            }}
            to {{
                opacity: 1;
                transform: translateY(0);
            }}
        }}

        .error-icon {{
            font-size: 80px;
            margin-bottom: 20px;
            animation: bounce 1s ease-in-out infinite;
        }}

        @keyframes bounce {{
            0%, 100% {{
                transform: translateY(0);
            }}
            50% {{
                transform: translateY(-20px);
            }}
        }}

        .error-status {{
            font-size: 48px;
            font-weight: bold;
            color: #667eea;
            margin-bottom: 10px;
        }}

        .error-title {{
            font-size: 28px;
            color: #333;
            margin-bottom: 15px;
            font-weight: 600;
        }}

        .error-message {{
            font-size: 16px;
            color: #666;
            margin-bottom: 30px;
            line-height: 1.6;
            word-wrap: break-word;
        }}

        .error-details {{
            background: #f8f9fa;
            border-left: 4px solid #667eea;
            padding: 15px;
            margin-bottom: 30px;
            border-radius: 8px;
            text-align: left;
            max-height: 300px;
            overflow-y: auto;
            font-size: 12px;
            color: #555;
            font-family: 'Courier New', monospace;
        }}

        .error-details::-webkit-scrollbar {{
            width: 6px;
        }}

        .error-details::-webkit-scrollbar-track {{
            background: #e0e0e0;
            border-radius: 3px;
        }}

        .error-details::-webkit-scrollbar-thumb {{
            background: #667eea;
            border-radius: 3px;
        }}

        .btn-back {{
            display: inline-block;
            padding: 12px 30px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            transition: all 0.3s ease;
            border: none;
            cursor: pointer;
            font-size: 16px;
        }}

        .btn-back:hover {{
            transform: translateY(-3px);
            box-shadow: 0 10px 20px rgba(102, 126, 234, 0.4);
            color: white;
        }}

        .btn-home {{
            display: inline-block;
            margin-left: 10px;
            padding: 12px 30px;
            background: #f0f0f0;
            color: #667eea;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            transition: all 0.3s ease;
            border: none;
            cursor: pointer;
            font-size: 16px;
        }}

        .btn-home:hover {{
            background: #e0e0e0;
            transform: translateY(-3px);
            color: #667eea;
        }}

        .status-codes {{
            font-size: 14px;
            color: #999;
            margin-top: 20px;
            padding-top: 20px;
            border-top: 1px solid #eee;
        }}

        @media (max-width: 600px) {{
            .error-container {{
                padding: 40px 20px;
            }}

            .error-icon {{
                font-size: 60px;
            }}

            .error-status {{
                font-size: 36px;
            }}

            .error-title {{
                font-size: 22px;
            }}

            .error-message {{
                font-size: 14px;
            }}

            .btn-back, .btn-home {{
                display: block;
                margin: 10px auto;
                width: 100%;
            }}

            .btn-home {{
                margin-left: 0;
            }}
        }}
    </style>
</head>
<body>
    <div class='error-container'>
        <div class='error-icon'>⚠️</div>
        <div class='error-status'>{statusCode}</div>
        <div class='error-title'>{System.Net.WebUtility.HtmlEncode(title)}</div>
        <div class='error-message'>{System.Net.WebUtility.HtmlEncode(message)}</div>
        {stackTraceHtml}
        <div>
            <button class='btn-back' onclick='history.back()'>← Вернуться назад</button>
            <a href='/' class='btn-home'>🏠 На главную</a>
        </div>
        <div class='status-codes'>
            Код ошибки: {statusCode}
        </div>
    </div>
</body>
</html>";
        }

        private string GetErrorTitle(int statusCode)
        {
            return statusCode switch
            {
                400 => "Неверный запрос",
                401 => "Требуется авторизация",
                403 => "Доступ запрещён",
                404 => "Страница не найдена",
                500 => "Внутренняя ошибка сервера",
                503 => "Сервис недоступен",
                _ => "Ошибка"
            };
        }

        private string GetErrorMessage(int statusCode)
        {
            return statusCode switch
            {
                400 => "Запрос содержит ошибку. Пожалуйста, проверьте данные и попробуйте снова.",
                401 => "Для доступа необходимо авторизоваться. Пожалуйста, войдите в систему.",
                403 => "У вас нет прав доступа к этой странице.",
                404 => "Страница, которую вы ищете, не была найдена. Пожалуйста, проверьте адрес URL.",
                500 => "Произошла внутренняя ошибка сервера. Пожалуйста, повторите попытку позже.",
                503 => "Сервис временно недоступен. Пожалуйста, попробуйте позже.",
                _ => "Произошла неиспользованная ошибка. Пожалуйста, повторите попытку позже."
            };
        }
    }
}

