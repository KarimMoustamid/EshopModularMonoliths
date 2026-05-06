using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Shared.Exceptions.Handler;

/// <summary>
/// This handler acts as a global exception trap in the ASP.NET Core pipeline. Its job is to:
/// 1. Catch any exception that bubbles up from application or domain layers
/// 2. Map domain-specific exception types (BadRequestException, NotFoundException, etc.) 
///    to appropriate HTTP status codes and error messages
/// 3. Serialize the response as a standardized ProblemDetails object (RFC 7807)
/// 4. Log the error for observability
/// 
/// This is a shared infrastructure component — all features depend on it, but it does not 
/// depend on any feature-specific code. This boundary is intentional: the handler must be 
/// exception-agnostic to the business logic.
/// 
/// Key design decision: Using an IExceptionHandler (asp.net core 8+) instead of custom 
/// middleware. This integrates with the framework's exception handling pipeline, meaning 
/// exceptions are caught before they can leak to the client and crash the connection.
/// </summary>
public class CustomExceptionHandler
    (ILogger<CustomExceptionHandler> logger)
    : IExceptionHandler
{
    // IExceptionHandler interface contract: TryHandleAsync returns true if handled, false if the exception should propagate
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Log the error immediately. We use DateTime.UtcNow to ensure consistent timestamps 
        // across distributed systems (not machine-local time). The exception itself is passed 
        // to Log error so structured logging captures the full stack trace.
        logger.LogError(
            "Error Message: {exceptionMessage}, Time of occurrence {time}",
            exception.Message, DateTime.UtcNow);

        // Pattern match on the exception type to determine the correct HTTP status code.
        // This tuple deconstruction assigns (Detail, Title, StatusCode) all at once.
        // Each pattern branch represents a semantic domain exception type that the application 
        // layer explicitly throws to signal a known error condition (validation failure, not found, etc).
        // The catch-all (_) case defaults to 500 because an unexpected exception is always an internal error.
        (string Detail, string Title, int StatusCode) details = exception switch
        {
            // 500 Internal Server Error — something failed inside the application that the code did not anticipate.
            // This is always a bug or an infrastructure failure, not user error.
            InternalServerException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status500InternalServerError
            ),
            // 400 Bad Request — the request failed validation before reaching business logic.
            // FluentValidation exceptions carry a list of field-level errors, which we attach 
            // to the response so the client knows exactly what to fix.
            ValidationException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            // 400 Bad Request — the request is malformed or violates a business rule check.
            // This is distinct from ValidationException: it fires after semantic validation, 
            // e.g. "user already exists" or "insufficient balance".
            BadRequestException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            // 404 Not Found — the resource the request targets does not exist.
            // This is not an error in the application; it is a client error (bad ID, wrong path).
            NotFoundException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status404NotFound
            ),
            // Catch-all: any exception type we did not explicitly handle defaults to 500.
            // This is a safety net. If a new exception type reaches here, it is logged and 
            // the client gets a generic error rather than seeing a stack trace or crashing.
            _ =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status500InternalServerError
            )
        };

        // Build a standardized error response following RFC 7807 (Problem Details).
        // ProblemDetails is the ASP.NET Core DTO for error responses. Its fields are:
        // - Title: a short human-readable summary (e.g. "Not Found")
        // - Detail: the specific error message (e.g. "User with ID 42 does not exist")
        // - Status: the HTTP status code
        // - Instance: the path of the request that failed (helps with debugging)
        var problemDetails = new ProblemDetails
        {
            Title = details.Title,
            Detail = details.Detail,
            Status = details.StatusCode,
            Instance = context.Request.Path
        };

        // Add the trace ID so support and debugging can correlate this error response 
        // back to the server logs. Every HTTP request gets a unique TraceIdentifier; 
        // including it in the error payload closes the loop between client and server logs.
        problemDetails.Extensions.Add("traceId", context.TraceIdentifier);

        // Special handling for ValidationException: attach the field-level validation errors 
        // so the client can render specific error messages for each invalid field.
        // This requires another type check; we already matched it above but extract the 
        // strongly-typed exception here to access the Errors collection.
        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions.Add("ValidationErrors", validationException.Errors);
        }

        // Serialize the problemDetails as JSON and write it directly to the response body.
        // This bypasses any further processing — the response is complete and ready to send.
        // The cancellationToken allows the operation to be cancelled (e.g. if the client 
        // disconnects before we finish writing).
        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);

        // Return true to signal that we handled this exception. The framework will not 
        // propagate it further. Returning false would allow other handlers to process it.
        return true;
    }
}
