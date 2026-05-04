
// ===================================================================
// This file defines IQuery<T>, the foundational contract for all 
// query requests in the CQRS pattern. Every query object in this 
// application must implement this interface.
//
// Why it exists:
// - Establishes a unified, discoverable contract that MediatR can use 
//   to route query objects to their handlers
// - The marker interface pattern (empty body) makes the intent explicit: 
//   "this is a query" — no additional behavior is needed
// - Inheriting from IRequest<T> connects the query to the MediatR pipeline, 
//   enabling cross-cutting concerns (logging, validation, caching) via behaviors
//
// Design decision: Why inherit from IRequest<T> and not just define 
// a standalone interface?
// - IRequest<T> is the MediatR contract; without it, the mediator cannot 
//   dispatch the query object to handlers
// - By inheriting, we avoid writing our own dispatcher — we reuse a mature library
//
// The `out` keyword (covariance):
// - Allows a handler that returns a more specific type to satisfy requests 
//   expecting a base type
// - Example: IQuery<Animal> can be satisfied by a handler returning Dog 
//   (if Dog : Animal)
// - This enables flexible, extensible handler registration
// ===================================================================

using MediatR;

namespace Shared.Contracts
{
    /// <summary>
    /// Marker interface for all query requests in the CQRS pattern.
    /// Inherits from MediatR's IRequest{TResponse} to enable dispatch 
    /// through the mediator pipeline.
    /// </summary>
    /// <typeparam name="T">The response type. Must be non-null (enforced by constraint).</typeparam>
    public interface IQuery<out T> : IRequest<T> where T : notnull
        // The `notnull` constraint ensures queries always return meaningful data,
        // never null. This is a deliberate design choice: queries must provide
        // a response object, even if it represents "no results" (e.g., an empty 
        // collection wrapped in a Result<T>). This forces the query author to 
        // think about the failure case explicitly.
    {
        // Intentionally empty.
        // The contract is purely structural: "I am a query that produces T."
        // Behavior is added via MediatR handlers and pipeline behaviors, not here.
    }

}