using System.Net;
using System.Text.Json;
using TreblePlayer.Services;

namespace TreblePlayer.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILoggingService _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILoggingService logger)
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
        _logger.LogError($"An unhandled exception occurred: {exception.Message}", exception);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            message = "An unexpected error occurred.",
            details = exception.Message // In production, you might want to hide this
        };

        var result = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(result);
    }
}
