// ErrorHandlingMiddleware.cs
using System.Net;
using System.Text.Json;
using AiMediaSync.API.Models;

namespace AiMediaSync.API.Middleware;

/// <summary>
/// Global error handling middleware
/// </summary>
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Error = "Internal server error",
            Timestamp = DateTime.UtcNow,
            RequestId = context.TraceIdentifier
        };

        switch (exception)
        {
            case FileNotFoundException ex:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.Error = "File not found";
                errorResponse.Details = ex.Message;
                break;

            case ArgumentException ex:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = "Invalid argument";
                errorResponse.Details = ex.Message;
                break;

            case UnauthorizedAccessException ex:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Error = "Unauthorized access";
                errorResponse.Details = ex.Message;
                break;

            case TimeoutException ex:
                response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.Error = "Request timeout";
                errorResponse.Details = ex.Message;
                break;

            case InvalidOperationException ex when ex.Message.Contains("Model not loaded"):
                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                errorResponse.Error = "Service temporarily unavailable";
                errorResponse.Details = "AI model is not loaded";
                errorResponse.SuggestedActions = new[] { "Try again in a few moments", "Contact support if issue persists" };
                break;

            case OutOfMemoryException ex:
                response.StatusCode = (int)HttpStatusCode.InsufficientStorage;
                errorResponse.Error = "Insufficient resources";
                errorResponse.Details = "Not enough memory to process the request";
                errorResponse.SuggestedActions = new[] { "Reduce file size", "Try processing in smaller batches" };
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Details = "An unexpected error occurred";
                errorResponse.SupportReference = GenerateSupportReference();
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(jsonResponse);
    }

    private string GenerateSupportReference()
    {
        return $"SUP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }
}

// LoggingMiddleware.cs
using System.Diagnostics;
using System.Text;

namespace AiMediaSync.API.Middleware;

/// <summary>
/// Request/response logging middleware
/// </summary>
public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;

        // Log request
        await LogRequestAsync(context, requestId);

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);

            // Copy response back to original stream
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);
        }
    }

    private async Task LogRequestAsync(HttpContext context, string requestId)
    {
        var request = context.Request;
        
        var logData = new
        {
            RequestId = requestId,
            Method = request.Method,
            Path = request.Path,
            QueryString = request.QueryString.ToString(),
            Headers = GetSafeHeaders(request.Headers),
            UserAgent = request.Headers.UserAgent.ToString(),
            RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
            ContentType = request.ContentType,
            ContentLength = request.ContentLength,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("HTTP Request: {@RequestData}", logData);

        // Log request body for non-file uploads (to avoid logging large files)
        if (ShouldLogRequestBody(request))
        {
            var body = await ReadRequestBodyAsync(request);
            if (!string.IsNullOrEmpty(body))
            {
                _logger.LogDebug("Request Body: {RequestId} - {Body}", requestId, body);
            }
        }
    }

    private async Task LogResponseAsync(HttpContext context, string requestId, long elapsedMs)
    {
        var response = context.Response;
        
        var logData = new
        {
            RequestId = requestId,
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            ContentLength = response.ContentLength,
            Headers = GetSafeHeaders(response.Headers),
            ElapsedMilliseconds = elapsedMs,
            Timestamp = DateTime.UtcNow
        };

        var logLevel = response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
        _logger.Log(logLevel, "HTTP Response: {@ResponseData}", logData);

        // Log response body for errors
        if (response.StatusCode >= 400 && ShouldLogResponseBody(response))
        {
            var body = await ReadResponseBodyAsync(context.Response);
            if (!string.IsNullOrEmpty(body))
            {
                _logger.LogDebug("Response Body: {RequestId} - {Body}", requestId, body);
            }
        }
    }

    private Dictionary<string, string> GetSafeHeaders(IHeaderDictionary headers)
    {
        var safeHeaders = new Dictionary<string, string>();
        var sensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Cookie", "Set-Cookie", "X-API-Key", "X-Auth-Token"
        };

        foreach (var header in headers)
        {
            if (sensitiveHeaders.Contains(header.Key))
            {
                safeHeaders[header.Key] = "[REDACTED]";
            }
            else
            {
                safeHeaders[header.Key] = header.Value.ToString();
            }
        }

        return safeHeaders;
    }

    private bool ShouldLogRequestBody(HttpRequest request)
    {
        if (request.ContentLength > 1024 * 1024) // Don't log bodies > 1MB
            return false;

        var contentType = request.ContentType?.ToLower() ?? "";
        
        // Don't log file uploads
        if (contentType.Contains("multipart/form-data") || 
            contentType.Contains("application/octet-stream"))
            return false;

        // Log JSON and form data
        return contentType.Contains("application/json") || 
               contentType.Contains("application/x-www-form-urlencoded") ||
               contentType.Contains("text/");
    }

    private bool ShouldLogResponseBody(HttpResponse response)
    {
        if (response.ContentLength > 1024 * 1024) // Don't log bodies > 1MB
            return false;

        var contentType = response.ContentType?.ToLower() ?? "";
        
        // Log JSON and text responses
        return contentType.Contains("application/json") || 
               contentType.Contains("text/");
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        try
        {
            request.EnableBuffering();
            
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            
            return body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body");
            return "[Failed to read body]";
        }
    }

    private async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        try
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            
            using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            
            return body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read response body");
            return "[Failed to read body]";
        }
    }
}