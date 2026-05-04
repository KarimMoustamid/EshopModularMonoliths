// ============================================================================
// Purpose: Centralize MediatR handler registration and cross-cutting concern wiring for the Modular Monolith
// This static class is the dependency injection seam that enables multiple modules to register their handlers
// into a single service collection without tight coupling. It discovers handlers by assembly reflection,
// ensuring new handlers are automatically wired once this extension is called at application startup.
// Design pattern: Extension method (fluent API) + Open Generic Behavior registration (CQRS infrastructure)
// ============================================================================

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Shared.Behaviors;
using System.Reflection;

namespace Shared.Extensions
{
    /// <summary>
    /// Static extension class for MediatR service collection configuration.
    /// This is the dependency injection composition root for CQRS handlers and behaviors across all modules.
    /// </summary>
    public static class MediatRExtentions
    {
        /// <summary>
        /// Discovers and registers all IRequest handlers, queries, and commands from one or more assemblies,
        /// along with validation and logging cross-cutting concerns.
        /// 
        /// This method enables the Modular Monolith pattern: modules define their own handlers in their own
        /// assemblies, but they are all wired together by passing those assemblies to this single extension method.
        /// The module that calls this method (usually the entry point / host) does not need to know the internals
        /// of any module—only which assemblies to scan.
        /// 
        /// Why we scan assemblies instead of registering manually:
        /// - Keeps module boundaries clean: no module needs to export its handler registrations
        /// - New handlers are automatically wired without modifying this code
        /// - Prevents circular dependency hell when modules reference each other's registrations
        /// 
        /// Why behaviors are registered here instead of in each module:
        /// - Validation and logging must wrap ALL handlers, regardless of which module owns them
        /// - Centralizing them ensures consistency: every handler gets the same treatment
        /// - If behaviors were scattered, some handlers might accidentally be missed
        /// </summary>
        /// <param name="services">The IServiceCollection to register into (fluent API)</param>
        /// <param name="assemblies">One or more assemblies to scan for IRequest handlers and validators</param>
        /// <returns>The same IServiceCollection for method chaining</returns>
        public static IServiceCollection AddMediatRWithAssemblies(this IServiceCollection services, params Assembly[] assemblies)
        {
            // Register MediatR and discover all CQRS handlers (IRequestHandler<TRequest, TResponse>) in the assemblies
            services.AddMediatR(config =>
            {
                // Scan the provided assemblies and automatically register any class that implements IRequestHandler<,>
                // This is the discovery mechanism that enables modules to be wired without explicit registration
                config.RegisterServicesFromAssemblies(assemblies);

                // Register ValidationBehavior<,> as an open generic behavior
                // This wraps every handler: before the handler executes, ValidationsRunner validates the request
                // Why open generic? Because we don't know at registration time what TRequest and TResponse types exist,
                // and we want validation to apply to ALL of them
                config.AddOpenBehavior(typeof(ValidationBehavior<,>));

                // Register LoggingBehavior<,> as an open generic behavior
                // This wraps every handler: logs the request before execution and logs the result after
                // Why open generic? Same reason: we want logging to apply to all handlers uniformly
                config.AddOpenBehavior(typeof(LoggingBehavior<,>));

            });
        }
    }
}