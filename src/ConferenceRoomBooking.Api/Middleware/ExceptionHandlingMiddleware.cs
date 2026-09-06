using System.Net;
using System.Text.Json;
using ConferenceRoomBooking.Domain.Exceptions;

namespace ConferenceRoomBooking.Api.Middleware;

/// <summary>
/// Converts exceptions into consistent JSON error responses (ProblemDetails-style) and makes sure
/// no unhandled exception ever leaks a stack trace to the client - important for API security/hardening.
/// Known domain exceptions map to sensible 4xx codes; anything unexpected becomes a generic 500
/// with no internal detail, while the full exception is still logged server-side for diagnostics.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            RoomNotFoundException or BookingNotFoundException => (HttpStatusCode.NotFound, "Not found"),
            RoomNotAvailableException => (HttpStatusCode.Conflict, "Room not available"),
            InvalidBookingRequestException or ArgumentException => (HttpStatusCode.BadRequest, "Invalid request"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Invalid request"),
            _ => (HttpStatusCode.InternalServerError, "Unexpected error")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Handled exception ({Title}) while processing {Method} {Path}: {Message}", title, context.Request.Method, context.Request.Path, exception.Message);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problem = new
        {
            title,
            status = (int)statusCode,
            // Domain exceptions carry safe, user-facing messages. Anything else is masked
            // so internal implementation details never reach the client.
            detail = statusCode == HttpStatusCode.InternalServerError ? "An unexpected error occurred. Please try again later." : exception.Message,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
