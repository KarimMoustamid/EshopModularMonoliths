// Purpose: Define a shared, reusable pagination contract that any query handler can embed
// to request results in pages. This avoids repeating PageIndex and PageSize properties across
// dozens of query classes. By centralizing pagination logic here, we ensure consistent
// naming and behavior across all modules. The record syntax provides value-based equality
// and immutability (via init-only properties), which is safe for passing between layers.

namespace Shared.Pagination
{
    /// <summary>
    /// Shared pagination contract used by query handlers to specify which page of results to retrieve.
    /// Encapsulates PageIndex (zero-based page number) and PageSize (number of items per page).
    /// By centralizing pagination here, we avoid repeating these two properties in every query class
    /// and ensure consistent naming and semantics across all modules.
    /// </summary>
    public record PaginationRequest(
        // Zero-based page number. Default 0 means the first page.
        // Set to 0 if the caller wants to retrieve from the beginning without specifying pagination.
        int PageIndex = 0,
        // Number of items per page. Default 10 is a reasonable starting point; callers can override.
        // The query handler is responsible for enforcing any business rules (e.g., max page size).
        int PageSize = 10);

}