using System.Diagnostics;
using System.Text;

namespace TgerCamera.Middleware;

/// <summary>
/// Middleware for logging HTTP requests and responses.
/// Logs request details (method, path, query string) and response information (status code, duration).
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the RequestLoggingMiddleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance for recording request/response information.</param>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to log HTTP request and response details.
    /// </summary>
    /// <param name="context">The HTTP context containing request and response information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;

        // Log incoming request
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
            // Call next middleware - response streams directly to client
            await _next(context);

            // Log response after completion
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
    /// Reads the request body from the HTTP request stream.
    /// </summary>
    /// <param name="request">The HTTP request object.</param>
    /// <returns>The request body as a string, or empty string if body cannot be read.</returns>
    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        // Only read body for requests that typically have one (POST, PUT, PATCH)
        if (!request.ContentLength.HasValue || request.ContentLength == 0)
        {
            return string.Empty;
        }

        request.EnableBuffering();
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            // Limit body logging to prevent excessive log sizes
            if (body.Length > 1000)
            {
                return body.Substring(0, 1000) + "... (truncated)";
            }

            return body;
        }
    }
}
