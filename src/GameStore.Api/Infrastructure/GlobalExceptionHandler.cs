using GameStore.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GameStore.Api.Infrastructure;

/// <summary>Equivalent of Spring's @ControllerAdvice + @ExceptionHandler. Returns RFC 9457 ProblemDetails.</summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        // Client disconnected: nothing to write back.
        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        int status;
        string title;
        string? detail;

        if (exception is AppException appException)
        {
            status = appException.StatusCode;
            title = appException.Title;
            detail = appException.Message;
            logger.LogWarning("Handled {ExceptionType}: {Message}", exception.GetType().Name, exception.Message);
        }
        else
        {
            status = StatusCodes.Status500InternalServerError;
            title = "An unexpected error occurred";
            detail = null;
            logger.LogError(exception, "Unhandled exception");
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }
        });
    }
}
