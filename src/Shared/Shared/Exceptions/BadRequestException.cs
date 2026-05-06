namespace Shared.Exceptions;

/// <summary>
/// This exception is thrown when a domain rule violation or validation failure occurs.
/// It is the application's contract for communicating to the presentation layer (API controllers)
/// that a request is invalid due to business logic constraints, not system errors.
/// 
/// Why this exists: Domain logic (Aggregates, Entities) must reject invalid state changes.
/// This exception is caught at the controller boundary and mapped to HTTP 400 Bad Request,
/// making the domain's rejection explicit and actionable to the caller without exposing
/// implementation details (database errors, internal state, stack traces).
/// 
/// Design intent: Every validation failure in the domain should throw this exception.
/// Controllers catch it and convert it to a standardized error response. This keeps
/// domain logic pure (no HTTP knowledge) while providing a clear signal that the error
/// is the client's responsibility to fix, not a system failure.
/// </summary>
public class BadRequestException : Exception
{
    /// <summary>
    /// Constructor with message only.
    /// Use this when the business rule violation is self-explanatory in the message.
    /// Example: throw new BadRequestException("Order total cannot be negative");
    /// </summary>
    public BadRequestException(string message) : base(message)
    {

    }


    /// <summary>
    /// Constructor with message and optional structured details.
    /// Use this when you need to communicate additional context to the caller
    /// (e.g., validation errors on specific fields, the invalid value that was provided).
    /// The Details property can be serialized to JSON in the HTTP response.
    /// Example: throw new BadRequestException("Invalid order", "quantity must be > 0");
    /// </summary>
    public BadRequestException(string mesasge, string details) : base(mesasge)
    {
        // Details is captured as a property so the controller can extract it
        // and include it in the HTTP 400 response body. This enables clients
        // to show field-level feedback without parsing the message string.
        Details = details;
    }


    /// <summary>
    /// Structured error details to send to the client.
    /// If null, only the Message is included in the error response.
    /// If populated, typically serialized as { "message": "...", "details": "..." }
    /// </summary>
    public string? Details { get; }
}
