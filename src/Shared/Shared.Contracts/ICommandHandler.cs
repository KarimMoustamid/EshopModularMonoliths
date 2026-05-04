// ============================================================================
// File: src/Shared/Shared.Contracts/CQRS/ICommandHandler.cs
//
// Purpose: Define the handler contracts that all CQRS command implementations
// must satisfy. This is the domain-language boundary — modules see these
// interfaces, not MediatR's IRequestHandler directly.
//
// Key roles:
// - ICommandHandler<in TCommand>: Fire-and-forget commands that return Unit
// - ICommandHandler<in TCommand, TResponse>: Commands that return a value
//
// Both delegate to MediatR's IRequestHandler, making MediatR the infrastructure
// while this contract defines what the domain expects. Handlers will implement
// one of these two based on whether the command needs to return a response.
// ============================================================================

// MediatR provides IRequestHandler, the base abstraction for all request/response
// mediator patterns. We layer our CQRS contracts on top of it to add domain semantics.
using MediatR;
namespace Shared.Contracts
{
    // Non-generic fire-and-forget command handler. TCommand must implement ICommand<Unit>
    // (no response payload). This interface inherits from the generic version, setting
    // TResponse to Unit. Unit is MediatR's way of saying "no return value" — it is a
    // marker type that satisfies the "where TResponse : notnull" constraint.
    // Handlers implementing this do not return a response; they perform side effects only.
    public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand<Unit>
    {
    }

    // Generic command handler with both command type and response type. Inherits from
    // MediatR's IRequestHandler, which means any handler implementing this must provide
    // a Handle(TCommand request, CancellationToken ct) method that returns Task<TResponse>.
    //
    // Constraints:
    // - TCommand : ICommand<TResponse> — enforces that the command declares what type
    //   it will return, preventing mismatches between command definition and handler.
    // - TResponse : notnull — prevents null response types, keeping the contract explicit.
    //
    // Why "in TCommand"? Contravariance allows a handler registered for a base command
    // type to handle derived command types. This is rarely used in practice but the
    // keyword is here because MediatR's request interface uses it.
    public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<ICommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
    {

    }
}