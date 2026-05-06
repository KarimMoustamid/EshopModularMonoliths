namespace Shared.Exceptions
{
    /// <summary>
    /// This exception represents an unexpected internal failure that indicates a bug, infrastructure issue, or
    /// unhandled edge case — not a domain validation failure or expected business exception. It is distinct from
    /// domain exceptions (which translate to 4xx responses) and exists to ensure we can distinguish between
    /// "the user did something wrong" (domain exception → 400/422) and "something broke inside" (InternalServerException → 500).
    ///
    /// The Details property is intentionally separate from the Message because:
    /// - Message is public-facing (will appear in API error responses)
    /// - Details is diagnostic context (logged, not sent to clients)
    /// This separation is crucial for security and clarity — we can safely log all details while exposing only
    /// the message to HTTP consumers.
    /// </summary>
    public class InternalServerException : Exception
    {
        /// <summary>
        /// Constructor for simple internal errors with only a message.
        /// Use this when the error message is self-contained and requires no additional diagnostic context.
        /// </summary>
        /// <param name="message">
        /// The public-facing error message. Keep this generic (e.g., "An unexpected error occurred")
        /// to avoid leaking implementation details to API clients.
        /// </param>
        public InternalServerException(string message) : base(message)
        {

        }

        /// <summary>
        /// Constructor for internal errors with both a public message and diagnostic details.
        /// Use this when you need to capture context (stack traces, state snapshots, timing info) for logging
        /// without including it in the HTTP response body.
        /// </summary>
        /// <param name="message">The public-facing error message sent to HTTP clients.</param>
        /// <param name="details">
        /// Diagnostic context string (e.g., "Outbox event publication failed after 3 retries: deadletter queue full").
        /// This is logged to observability systems but never sent to the client. This is why we store it separately
        /// from the base Exception.Message — the base Message stays generic, but Details captures the actual problem.
        /// </param>
        public InternalServerException(string message, string details) : base(message)
        {
            Details = details;
        }

        /// <summary>
        /// Diagnostic context for this error. Always null-check this before use.
        /// This property exists because Exception.Message is already claimed by the base class for the public message.
        /// By separating concerns here, API exception handlers can safely log Details to structured logs while
        /// returning only Message to HTTP clients.
        /// </summary>
        public string? Details { get; set; }
    }
}