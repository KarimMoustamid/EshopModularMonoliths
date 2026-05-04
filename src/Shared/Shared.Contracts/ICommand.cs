// ================================================
// Purpose: Define the base contract for all command requests in the system.
// This file establishes the CQRS command abstraction by inheriting from MediatR's IRequest.
// Every command in the system must implement ICommand or ICommand<TResponse>.
// 
// Why it exists at this layer:
// The Shared.Contracts assembly is the only assembly that other modules are allowed to depend on.
// By defining ICommand here, we ensure that commands are decoupled from implementation details.
// Modules can send commands without knowing which handler will process them or where the handler lives.
//
// Design decision: covariance (out keyword)
// The out keyword on TResponse enables covariance: a handler for ICommand<Animal> can be used
// where ICommand<Dog> is expected (Dog is a subtype of Animal). This is crucial for plugin
// architectures where derived command types should be assignable to base command types.
// Without covariance, TResponse would be invariant, breaking substitutability.
//
// What comes next: In later phases, handlers will implement ICommandHandler<TCommand, TResponse>
// (from MediatR) and the dispatcher will resolve and invoke them via the MediatR pipeline.
// ================================================

using MediatR;

namespace Shared.Contracts
{
    // ICommand is the non-generic marker interface for commands that return Unit (void).
    // Use this when the command side effect is the point (like "CreateUser") and you don't need a response.
    // It is shorthand for ICommand<Unit>, providing a cleaner API for void-returning commands.
    public interface ICommand : ICommand<Unit>
    {

    }

    // ICommand<out TResponse> is the generic base contract for all commands.
    // The 'out' keyword declares TResponse as covariant: if Dog : Animal, then ICommand<Dog> : ICommand<Animal>.
    // This allows handlers for base command types to accept derived command types without explicit casting.
    // TResponse is the type of value the command execution will return (e.g., UserId, OrderId, or Unit for void).
    // Inheriting from IRequest<TResponse> plugs this command into MediatR's mediation pipeline:
    // MediatR uses reflection to find ICommandHandler<TCommand, TResponse> implementations at runtime.
    public interface ICommand<out TResponse> : IRequest<TResponse>
    {

    }
}