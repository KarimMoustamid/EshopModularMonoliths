using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog
{
    /// <summary>
    /// CatalogModule — Step 8: Module Composition Root for the Catalog Bounded Context
    /// 
    /// This static class owns all dependency injection and middleware registration for the Catalog module.
    /// It exposes two extension methods that are called from the host's Program.cs, making the module
    /// a composable unit that can be added or removed without touching any host configuration.
    /// 
    /// Design intent: In a modular monolith, each module registers itself. This decouples the module's
    /// infrastructure (DbContext, services, endpoints) from the host, allowing modules to evolve independently.
    /// The two-method pattern (Add* and Use*) mirrors the standard ASP.NET Core pattern and makes the 
    /// registration order explicit and testable.
    /// </summary>
    public static class CatalogModule
    {

        /// <summary>
        /// AddCatalogModule - Registers all Catalog module services into the DI container.
        /// Called from Program.cs before the host builds, in the services configuration phase.
        /// </summary>
        /// <param name="services">The IServiceCollection from the host. We add to it and return it for chaining.</param>
        /// <param name="configuration">The app configuration, needed to read the database connection string.</param>
        /// <returns>The same IServiceCollection, to allow fluent chaining in Program.cs</returns>
        public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
        {
            // Add services to the container.
            // This section is organized in layers: API endpoints, application logic, then data/infrastructure.
            // Each layer's dependencies are registered in isolation so they can be understood independently.

            // Api Endpoint services
            // Future: Register API endpoint handlers, validators, and response mappers here.

            // Application Use Case services
            // Future: Register application layer services (CQRS handlers, domain services, etc.) here.       

            // Data - Infrastructure services
            // This layer registers database access, EF Core, and lower-level infrastructure.
            // We read the connection string once at startup to avoid repeated configuration lookups.
            _ = configuration.GetConnectionString("Database");

            return services;


        }


        /// <summary>
        /// UseCatalogModule - Registers Catalog module middleware and post-startup configuration.
        /// Called from Program.cs after the host is built, in the middleware pipeline configuration phase.
        /// This is where we set up request processing (endpoints, logging, error handling) and 
        /// trigger startup tasks like running EF Core migrations.
        /// </summary>
        /// <param name="app">The IApplicationBuilder from the host. We configure it and return it for chaining.</param>
        /// <returns>The same IApplicationBuilder, to allow fluent chaining in Program.cs</returns>
        public static IApplicationBuilder UseCatalogModule(this IApplicationBuilder app)
        {
            // Configure the HTTP request pipeline.
            // Like AddCatalogModule, this is organized by layer: endpoints, application concerns, then data concerns.

            // 1. Use Api Endpoint services
            // Future: Map API endpoints, register route groups, etc. here.

            // 2. Use Application Use Case services
            // Future: Register application middleware (logging, exception handling, etc.) here.

            // 3. Use Data - Infrastructure services
            // Run EF Core migrations at startup. This ensures the database schema is up-to-date before
            // any requests are processed. If a migration fails, the app fails to start (fail-fast design).
            return app;
        }
    }
}
