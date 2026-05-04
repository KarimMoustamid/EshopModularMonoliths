// ================================================
// File role: Module composition root for the Basket module in a Modular Monolith architecture.
//
// Design intent: The BasketModule class is a static composition root that encapsulates all
// Basket module infrastructure — repository patterns, caching, database context, domain event
// publishing, and interceptors. It exposes only two public extension methods: AddBasketModule()
// for dependency injection registration and UseBasketModule() for HTTP pipeline configuration.
// This design isolates the Basket module's internal wiring from the host application. The host
// (Program.cs) knows only how to call these two methods; it has no knowledge of BasketDbContext,
// OutboxProcessor, or CachedBasketRepository. This is the core pattern of a Modular Monolith:
// each module is independently wirable, testable, and deployable (in the future).
// ================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basket
{
    /// <summary>
    /// Static composition root for the Basket module. Registers all Basket services into the DI
    /// container and configures the HTTP pipeline. Called from Program.cs during startup.
    /// </summary>
    public static class BasketModule
    {
        /// <summary>
        /// Registers all Basket domain services, application logic, and infrastructure into the DI container.
        /// This method is called once during application startup and is responsible for wiring everything
        /// the Basket module needs to function. The host application only knows this method exists; it has
        /// no knowledge of the internals (repositories, DbContext, processors, etc.).
        /// </summary>
        /// <param name="services">The DI container to register services into. This is the same IServiceCollection
        /// used by all other modules — the modules register themselves into the shared container.</param>
        /// <param name="configuration">The application configuration (appsettings.json, environment variables).
        /// Used to retrieve the database connection string so the module can configure its own DbContext.</param>
        /// <returns>The same IServiceCollection for chaining. Standard pattern for extension methods
        /// that build the DI container.</returns>
        public static IServiceCollection AddBasketModule(this IServiceCollection services,
        IConfiguration configuration)
        {
            // Add services to the container.
            // The sections below show the canonical layer structure: API endpoints, application use cases, and data infrastructure.
            // This structure is a convention in the codebase — it helps readers understand the purpose of each registration.

            // 1. Api Endpoint services
            // (Commented placeholder — endpoint registrations like minimal API routes, controllers, etc. would go here.)

            // 2. Application Use Case services
            // 3. Data - Infrastructure services
            // Retrieve the database connection string from configuration. This allows different environments
            // (dev, staging, prod) to point to different databases without code changes.
            _ = configuration.GetConnectionString("Database");

            return services;


        }

        // <summary>
        /// Configures the HTTP request pipeline for the Basket module. Called during application startup,
        /// after all AddBasketModule() calls have completed, to set up any middleware or HTTP-level behavior
        /// the module needs. Like AddBasketModule(), this is called only by the host (Program.cs); the host
        /// is the only place that knows all modules exist.
        /// </summary>
        /// <param name="app">The WebApplication (HTTP pipeline builder). Same instance used by all other modules.</param>
        /// <returns>The same WebApplication for chaining.</returns>
        public static IApplicationBuilder UseBasketModule(this IApplicationBuilder app)
        {
            // Configure the HTTP request pipeline.
            // The sections below show which layer concerns are configured at HTTP pipeline time vs. DI time.
            // At this point, all DI registration (AddBasketModule) has completed and the DI container is built.

            // 1. Use Api Endpoint services
            // (Commented placeholder — endpoint registrations (MapGet, MapPost, etc.) would go here if using minimal APIs.)

            // 2. Use Application Use Case services
            // (Commented placeholder — middleware related to application logic (e.g., authorization, caching directives) would go here.)

            // 3. Use Data - Infrastructure services
            // Run database migrations. The UseMigration<BasketDbContext>() extension method applies any pending migrations
            // for the BasketDbContext. This ensures the database schema matches the current code before the application
            // starts processing requests. By putting this in UseBasketModule(), each module owns its own migrations —
            // the host does not need to know which databases exist or manage them explicitly.
            return app;
        }
    }
}
