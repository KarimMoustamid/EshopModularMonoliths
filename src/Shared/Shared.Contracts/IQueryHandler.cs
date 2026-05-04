// ===================================================================
// Location: src/Shared/Shared.Contracts/CQRS/IQueryHandler.cs
// Purpose: Define the contract that all query handlers must implement.
// 
// In CQRS, queries are read operations that return data without side effects.
// This interface ensures type safety by requiring:
// 1. TQuery must be an IQuery<TResponse> (our project's query marker interface)
// 2. TResponse must be non-null (enforces that queries always return a value)
// 3. All handlers inherit from MediatR's IRequestHandler (provides the execution pipeline)
//
// Why this exists: Queries need a different contract than commands because they have
// different guarantees (no side effects, always return data, idempotent).
// This interface makes that distinction explicit and prevents accidentally returning
// null from a query, which would be a silent failure.
// ===================================================================
using MediatR;

namespace Shared.Contracts
{
    /// <summary>
    /// Contract for CQRS query handlers.
    /// All query handlers in this project must implement this interface.
    /// TQuery is contravariant (in) because a handler accepting a base query type
    /// can safely handle derived query types.
    /// </summary>
    public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    // Constraint: TQuery must be an IQuery marked with a specific TResponse type.
    // This ensures the query and handler agree on the response type at compile time.
    where TQuery : IQuery<TResponse>
    // Constraint: TResponse must be non-null. Queries must always return a value.
    // If a query finds no data, throw an exception; do not return null.
    // This prevents silent failures and makes the read contract explicit.
    where TResponse : notnull
    {
        // This interface is intentionally empty.
        // It serves as a marker contract that inherits from MediatR's IRequestHandler.
        // The actual execution logic (Handle method) is defined in IRequestHandler.
        // Any class implementing this interface must provide a Handle() method that:
        // - Takes a TQuery and CancellationToken
        // - Returns Task<TResponse> (never null, due to the where TResponse : notnull constraint)
        // 
        // Why inherit IRequestHandler instead of defining Handle here?
        // Because MediatR's IRequestHandler already defines the correct signature.
        // We inherit it to leverage MediatR's middleware pipeline (logging, validation, caching, etc.)
        // and to ensure all handlers follow the same execution contract.

    }
}