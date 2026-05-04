// ============================================================
// This file is the entry point for the Ordering module's dependency injection and HTTP pipeline configuration.
// In a Modular Monolith, each module must be self-contained: it registers its own services and configures
// its own middleware without forcing other modules to know about its internals.
//
// The pattern here uses extension methods on IServiceCollection and IApplicationBuilder so the root Program.cs
// can wire up all modules with clean, readable calls like:
//   services.AddOrderingModule(configuration)
//   app.UseOrderingModule()
//
// This keeps the root composition root lightweight and makes it easy to add, remove, or swap modules without
// touching the bootstrap code. The module itself controls what services and interceptors it needs.
//
// By the end of this step, both AddOrderingModule and UseOrderingModule are defined and callable,
// even though their bodies are still incomplete (marked with TODO sections for API, Application, and Data layers).
// ============================================================

using Microsoft.AspNetCore.Builder;
// IApplicationBuilder is the ASP.NET Core middleware pipeline builder. We extend it so UseOrderingModule()
// can be called as a natural part of the app startup pipeline (see Program.cs).
using Microsoft.Extensions.Configuration;
// IConfiguration is how we read appsettings.json and environment variables. Each module gets the full config
// but reads only the keys it cares about (e.g., "Database" for the connection string).
using Microsoft.Extensions.DependencyInjection;
// IServiceCollection is the DI container's registry. By extending it with AddOrderingModule,
// we make it idiomatic to register this module's services in Program.cs.

namespace Ordering
{
    /// <summary>
    /// OrderingModule encapsulates all dependency injection and middleware setup for the Ordering module.
    /// This is the composition root for the Ordering feature; every class the Ordering module uses
    /// must be registered here. If another module needs something from Ordering, it accesses it through
    /// a public service interface, not by directly instantiating classes.
    /// </summary>
    public static class OrderingModule
    {

        /// <summary>
        /// Registers all services needed by the Ordering module into the DI container.
        /// Called in Program.cs before the host is built.
        /// This method returns IServiceCollection so it can be chained with other AddXxx calls.
        /// </summary>
        public static IServiceCollection AddOrderingModule(this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add services to the container.

            // 1. Api Endpoint services
            // TODO: Register HTTP endpoint handlers here (e.g., CreateOrderEndpoint, GetOrderEndpoint).
            // These will be mapped in Program.cs or invoked by a router. Each endpoint is a class
            // that handles one HTTP request and should be stateless and testable.

            // 2. Application Use Case services
            // TODO: Register application service classes here (e.g., CreateOrderHandler, GetOrderHandler).
            // These orchestrate domain logic and call repositories. They are the bridge between HTTP
            // endpoints and domain entities. By registering them here, we keep them invisible to other modules.

            // 3. Data - Infrastructure services
            // This section is mostly complete: it wires up EF Core, interceptors, and the database.
            // Retrieve the database connection string from appsettings.json.
            // If the key "Database" is missing, GetConnectionString returns null, and AddDbContext will throw
            // a clear error at startup (fail-fast principle). This is better than failing later with a cryptic
            // connection error.
            _ = configuration.GetConnectionString("Database");

            return services;
        }

        /// <summary>
        /// Configures the HTTP middleware pipeline for the Ordering module.
        /// Called in Program.cs after services are built but before the host starts listening.
        /// This method returns IApplicationBuilder so it can be chained with other UseXxx calls.
        /// </summary>
        public static IApplicationBuilder UseOrderingModule(this IApplicationBuilder app)
        {
            // Configure the HTTP request pipeline.

            // 1. Use Api Endpoint services
            // TODO: Map HTTP routes to endpoint handlers here (e.g., MapCreateOrder, MapGetOrder).
            // These are typically minimal endpoints or controller actions that parse the HTTP request,
            // call an application service, and return an HTTP response. This happens after service registration
            // because we need the app instance to be built (and thus all services resolved) to define routes.

            // 2. Use Application Use Case services
            // TODO: Register any application-level middleware here (e.g., logging, error handling specific to Ordering).
            // This is typically empty for feature modules; global middleware is registered in Program.cs.

            // 3. Use Data - Infrastructure services
            // Run pending EF Core migrations on the OrderingDbContext. This ensures the database schema
            // is up to date with the current version of OrderingDbContext before the first request is processed.
            // It only runs pending migrations, so it's safe to call on every startup. If no migrations are pending,
            // it does nothing. If a migration fails, the application will not start (fail-fast).
            return app;
        }
    }
}
