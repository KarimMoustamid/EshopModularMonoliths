namespace Shared.Exceptions
{
    /// <summary>
    /// Exception thrown when a requested resource cannot be found.
    /// This is mapped to HTTP 404 in the exception handler.
    /// </summary>
    public class NotFoundException : Exception
    {
        /// <summary>
        /// Constructor for a missing resource error with only a public-facing message.
        /// Use this when the message is sufficient to describe the missing resource.
        /// </summary>
        /// <param name="message">A human-readable error message for API consumers.</param>
        public NotFoundException(string message) : base(message)
        {
        }

        /// <summary>
        /// Constructor for a missing resource error that includes diagnostic details.
        /// Use this when additional context should be logged but not exposed to clients.
        /// </summary>
        /// <param name="message">The public-facing error message.</param>
        /// <param name="details">Optional diagnostic details for structured logging.</param>
        public NotFoundException(string message, string details) : base(message)
        {
            Details = details;
        }

        /// <summary>
        /// Optional diagnostic details for the missing resource error.
        /// These details are intended for logs and diagnostics, not for client-facing error text.
        /// </summary>
        public string? Details { get; }
    }
}
