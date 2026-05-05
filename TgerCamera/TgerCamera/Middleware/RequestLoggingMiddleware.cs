using System.Diagnostics;
using System.Text;

namespace TgerCamera.Middleware;

/// <summary>
/// Middleware dùng để log HTTP requests và responses.
/// Ghi lại chi tiết request (method, path, query string) và thông tin response (status code, duration).
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>
    /// Khởi tạo một instance mới của RequestLoggingMiddleware.
    /// </summary>
    /// <param name="next">Middleware kế tiếp trong pipeline.</param>
    /// <param name="logger">Logger instance dùng để ghi thông tin request/response.</param>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Gọi middleware để log chi tiết HTTP request và response.
    /// </summary>
    /// <param name="context">HTTP context chứa thông tin request và response.</param>
    /// <returns>Một task đại diện cho asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;

        // Log request đi vào
        var requestBody = await ReadRequestBodyAsync(request);
        _logger.LogInformation(
            "HTTP Request: {Method} {Path}{QueryString} - Content-Type: {ContentType}",
            request.Method,
            request.Path,
            request.QueryString,
            request.ContentType);

        if (!string.IsNullOrEmpty(requestBody))
        {
            _logger.LogDebug("Request Body: {RequestBody}", requestBody);
        }

        try
        {
            // Gọi middleware tiếp theo - response sẽ stream trực tiếp về client
            await _next(context);

            // Log response sau khi hoàn tất
            stopwatch.Stop();
            _logger.LogInformation(
                "HTTP Response: {Method} {Path} - Status Code: {StatusCode} - Duration: {Duration}ms",
                request.Method,
                request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "HTTP Request Error: {Method} {Path} - Status Code: {StatusCode} - Duration: {Duration}ms",
                request.Method,
                request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Đọc request body từ HTTP request stream.
    /// </summary>
    /// <param name="request">Đối tượng HTTP request.</param>
    /// <returns>Request body ở dạng string, hoặc empty string nếu body không thể đọc được.</returns>
    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (IsSensitivePath(request.Path))
        {
            return string.Empty;
        }

        // Chỉ đọc body cho các requests thường có body (POST, PUT, PATCH)
        if (!request.ContentLength.HasValue || request.ContentLength == 0)
        {
            return string.Empty;
        }

        request.EnableBuffering();
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            // Giới hạn việc log body để tránh log quá lớn
            if (body.Length > 1000)
            {
                return body.Substring(0, 1000) + "... (truncated)";
            }

            return body;
        }
    }

    private static bool IsSensitivePath(PathString path)
    {
        return path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/google-login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/orders/checkout", StringComparison.OrdinalIgnoreCase);
    }
}
