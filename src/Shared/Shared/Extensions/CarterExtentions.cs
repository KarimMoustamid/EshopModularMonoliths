// ============================================================================
// This file demonstrates the Module Discovery pattern in a Modular Monolith.
// Carter is an HTTP framework that routes requests to handler classes called Modules.
// Rather than registering each module by name in the composition root (brittle and verbose),
// this extension uses reflection to find all ICarterModule implementations in given assemblies.
// The caller passes in the assemblies to scan; this method handles the discovery and registration.
// This keeps module implementations decoupled from the DI setup — a new module is auto-discovered just by existing.
// ============================================================================


using System.Reflection;
using Carter;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Extensions
{
    /// <summary>
    /// Extension methods for Carter HTTP module registration.
    /// Enables automatic discovery and wiring of Carter modules across assemblies without explicit name-based registration.
    /// </summary>
    public static class CarterExtentions
    {
        /// <summary>
        /// Discovers all ICarterModule implementations in the specified assemblies and registers them with the DI container.
        /// </summary>
        /// <param name="services">The IServiceCollection to register modules into.</param>
        /// <param name="assemblies">One or more assemblies to scan for module implementations. Typically includes feature/domain assemblies.</param>
        /// <returns>The IServiceCollection for fluent chaining.</returns>
        /// <remarks>
        /// This method uses reflection to find all types that implement ICarterModule in each provided assembly.
        /// It then passes them to Carter's configurator, which registers them as route handlers.
        /// This avoids the need for hardcoded module registrations in the composition root and enables the module discovery pattern:
        /// a developer adds a new module to an assembly, and it is automatically registered without any wiring code.
        /// </remarks>
        public static IServiceCollection AddCarterWithAssemblies(this IServiceCollection services, params Assembly[] assemblies)
        {
            // Delegate to Carter's built-in AddCarter method, but wrap it with our discovery logic.
            // We pass a configurator function that will be called by Carter during setup.
            services.AddCarter(configurator: config =>
            {
                // For each assembly the caller passed in, discover all module types.
                foreach (var assembly in assemblies)
                {
                    // Use reflection to find all types in this assembly that implement the ICarterModule interface.
                    // GetTypes() returns all types in the assembly; Where filters to those assignable to ICarterModule.
                    // ToArray() materializes the results so we can pass them to WithModules in one batch. 
                    var modules = assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(ICarterModule))).ToArray();

                    // Register all discovered modules with Carter's configuration.
                    // WithModules() tells Carter which module classes to instantiate and wire as route handlers.
                    config.WithModules(modules);
                }
            });

            // Return the services collection to enable fluent method chaining.
            // This allows the caller to do: services.AddCarterWithAssemblies(...).AddOtherService(...).
            return services;
        }

    }
}
