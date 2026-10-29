namespace GameStore.Api.Exceptions;

/// <summary>Base type for expected, client-facing errors. Mapped to ProblemDetails by GlobalExceptionHandler.</summary>
public abstract class AppException(string message, int statusCode, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

public sealed class NotFoundException(string entity, object key)
    : AppException($"{entity} with id '{key}' was not found.", StatusCodes.Status404NotFound, "Resource not found");

public sealed class BadRequestException(string message)
    : AppException(message, StatusCodes.Status400BadRequest, "Bad request");

public sealed class ConflictException(string message)
    : AppException(message, StatusCodes.Status409Conflict, "Conflict");

public sealed class UnauthorizedException(string message)
    : AppException(message, StatusCodes.Status401Unauthorized, "Unauthorized");
