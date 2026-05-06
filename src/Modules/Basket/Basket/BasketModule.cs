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

// These imports bring in the types needed to register services and configure the HTTP pipeline.
// Each import represents a concern: repositories and processors (business logic), interceptors
// (cross-cutting infrastructure), and DI/ASP.NET types (wiring framework).
using Microsoft.AspNetCore.Builder;
//TODO: using Basket.Data.Processors;
//TODO: using Basket.Data.Repository;
//TODO: using Microsoft.EntityFrameworkCore.Diagnostics;
//TODO: using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
//TODO: using Shared.Data;
//TODO: using Shared.Data.Interceptors;

namespace Basket;

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
        // Repository pattern: define a contract (IBasketRepository) and register concrete implementations.
        // The Decorate<> call wraps the base repository with a caching layer, implementing the Decorator pattern.
        // This means when code requests IBasketRepository, it gets CachedBasketRepository, which delegates
        // to BasketRepository but intercepts reads to check the cache first. This is how cross-cutting concerns
        // (caching) are layered on top of business logic without modifying the core repository.
        //TODO: services.AddScoped<IBasketRepository, BasketRepository>();
        //TODO: services.Decorate<IBasketRepository, CachedBasketRepository>();

        // 3. Data - Infrastructure services
        // Retrieve the database connection string from configuration. This allows different environments
        // (dev, staging, prod) to point to different databases without code changes.
        var connectionString = configuration.GetConnectionString("Database");

        // Register SaveChanges interceptors. These are EF Core hooks that run when SaveChangesAsync() is called.
        // Why register them separately as ISaveChangesInterceptor? Because AddDbContext later retrieves them
        // and passes them to options.AddInterceptors(). This allows the module to compose its own interceptors
        // without the calling code knowing about them. The host doesn't need to know that AuditableEntityInterceptor
        // and DispatchDomainEventsInterceptor exist.
        //TODO: services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        //TODO: services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        // Register the BasketDbContext. The factory function (sp, options) is called when the DbContext is instantiated.
        // It retrieves all registered ISaveChangesInterceptor instances from the DI container and adds them to the
        // DbContext options. This pattern decouples the DbContext configuration from the interceptor definitions — 
        // new interceptors can be added (by registering them as ISaveChangesInterceptor) without changing the DbContext.
        // UseNpgsql() configures EF Core to use PostgreSQL as the database provider.
        //TODO: services.AddDbContext<BasketDbContext>((sp, options) =>
        //TODO: {
        //TODO:     options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
        //TODO:     options.UseNpgsql(connectionString);
        //TODO: });

        // Register OutboxProcessor as a hosted service. A hosted service is a long-running background task
        // that starts when the application starts and stops when the application stops. OutboxProcessor
        // is responsible for publishing domain events that were queued (in the Outbox table) during the last
        // transaction. Registering it here means the Basket module "owns" its own event publishing; the host
        // doesn't need to know about the Outbox pattern or event publishing.
        //TODO: services.AddHostedService<OutboxProcessor>();

        return services;
    }

    /// <summary>
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
