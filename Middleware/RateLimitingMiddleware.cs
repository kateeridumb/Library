using System.Collections.Concurrent;

namespace LibraryMPT.Middleware
{
    /// <summary>
    /// Middleware для защиты от DDoS атак через rate limiting
    /// Ограничивает количество запросов с одного IP адреса
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        
        // Словарь для хранения количества запросов по IP адресам
        private static readonly ConcurrentDictionary<string, RateLimitInfo> _requestCounts = new();
        
        // Настройки rate limiting
        private const int MaxRequestsPerMinute = 60; // Максимум 60 запросов в минуту
        private const int MaxRequestsPerSecond = 10; // Максимум 10 запросов в секунду
        private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _secondWindow = TimeSpan.FromSeconds(1);

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = GetClientIpAddress(context);
            
            // Пропускаем статические файлы и health checks
            if (IsExcludedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var now = DateTime.UtcNow;
            var rateLimitInfo = _requestCounts.GetOrAdd(clientIp, _ => new RateLimitInfo());

            bool shouldBlock = false;
            int retryAfter = 1;
            string blockReason = "";

            lock (rateLimitInfo)
            {
                // Очистка старых записей
                CleanupOldRequests(rateLimitInfo, now);

                // Проверка лимита в секунду
                var recentRequests = rateLimitInfo.Requests.Count(r => r > now - _secondWindow);
                if (recentRequests >= MaxRequestsPerSecond)
                {
                    shouldBlock = true;
                    blockReason = "per second";
                }

                // Проверка лимита в минуту
                if (!shouldBlock)
                {
                    var minuteRequests = rateLimitInfo.Requests.Count(r => r > now - _timeWindow);
                    if (minuteRequests >= MaxRequestsPerMinute)
                    {
                        shouldBlock = true;
                        blockReason = "per minute";
                        var oldestRequest = rateLimitInfo.Requests
                            .Where(r => r > now - _timeWindow)
                            .OrderBy(r => r)
                            .FirstOrDefault();
                        if (oldestRequest != default)
                        {
                            retryAfter = (int)Math.Ceiling((oldestRequest - (now - _timeWindow)).TotalSeconds);
                        }
                    }
                }

                if (!shouldBlock)
                {
                    // Добавляем текущий запрос
                    rateLimitInfo.Requests.Add(now);
                }
            }

            if (shouldBlock)
            {
                _logger.LogWarning("Rate limit exceeded ({Reason}) for IP: {IpAddress}", blockReason, clientIp);
                context.Response.StatusCode = 429;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Retry-After", retryAfter.ToString());
                await context.Response.WriteAsync($$"""
                    {
                        "error": "Слишком много запросов. Пожалуйста, подождите немного.",
                        "retryAfter": {{retryAfter}}
                    }
                    """);
                return;
            }

            await _next(context);
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Проверяем заголовки прокси
            var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip.Split(',')[0].Trim();
            }

            ip = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private bool IsExcludedPath(PathString path)
        {
            var excludedPaths = new[]
            {
                "/css/",
                "/js/",
                "/lib/",
                "/favicon.ico",
                "/images/",
                "/books/"
            };

            return excludedPaths.Any(excluded => path.Value?.StartsWith(excluded, StringComparison.OrdinalIgnoreCase) == true);
        }

        private void CleanupOldRequests(RateLimitInfo rateLimitInfo, DateTime now)
        {
            // Удаляем запросы старше окна времени
            rateLimitInfo.Requests.RemoveAll(r => r < now - _timeWindow);
        }

        private class RateLimitInfo
        {
            public List<DateTime> Requests { get; } = new List<DateTime>();
        }
    }
}

