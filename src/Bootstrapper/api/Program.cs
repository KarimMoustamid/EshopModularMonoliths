// ══════════════════════════════════════════════════════════════════════════════════
// Program.cs — Bootstrap Configuration
// ══════════════════════════════════════════════════════════════════════════════════
// 
// This file is the ASP.NET Core entry point (Minimal Hosting Model).
// It configures services (DI), middleware (request pipeline), and module initialization.
// 
// In Phase 2, this file is extended to wire in the shared DDD abstractions:
// - CustomExceptionHandler (Step 9) catches exceptions from any module or endpoint and 
//   returns standardized ProblemDetails responses
// - All exception types (BadRequestException, InternalServerException) are available 
//   because they compile with the shared DDD layer
// - The exception handler is the single enforcement point for consistent error responses

// ══════════════════════════════════════════
// STEP 1 — Verify prerequisites from Phase 1
// ══════════════════════════════════════════
// This entire Program.cs file must compile. It references:
// - CatalogModule, BasketModule, OrderingModule (module pattern from Phase 1)
// - CarterExtensions, MediatRExtensions (CQRS setup from Phase 1)
// - Configuration binding (appsettings.json from Phase 1)
// The step is done when `dotnet build` shows "Build succeeded."

using Shared.Exceptions.Handler;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════
// Logging configuration (Phase 1 prerequisite)
// ══════════════════════════════════════════
// Serilog is configured from appsettings.json. This ensures structured logging
// is available throughout the application and in the exception handler.
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Add services to the container.

// ══════════════════════════════════════════
// Module assembly references (Phase 1 prerequisite)
// ══════════════════════════════════════════
// These are needed to scan each module's namespace for Carter endpoints and MediatR handlers.
// Each module is responsible for its own feature logic (Catalog, Basket, Ordering).
var catalogAssembly = typeof(CatalogModule).Assembly;
var basketAssembly = typeof(BasketModule).Assembly;
var orderingAssembly = typeof(OrderingModule).Assembly;

// ══════════════════════════════════════════
// Carter registration (Phase 1 prerequisite)
// ══════════════════════════════════════════
// Registers all Carter endpoints from each module assembly.
// Carter is the lightweight routing abstraction that maps HTTP requests to ICarterModule handlers.
builder.Services.AddCarterWithAssemblies(catalogAssembly, basketAssembly, orderingAssembly);


// ══════════════════════════════════════════
// MediatR registration (Phase 1 prerequisite)
// ══════════════════════════════════════════
// Registers all CQRS handlers (ICommandHandler<>, IQueryHandler<>) from each module.
// This is how endpoints invoke domain logic without direct coupling.
builder.Services.AddMediatRWithAssemblies(catalogAssembly, basketAssembly, orderingAssembly);


// ══════════════════════════════════════════
// Redis cache (Phase 1 prerequisite)
// ══════════════════════════════════════════
// Distributed cache for cross-module state (e.g., catalog caching).
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// ══════════════════════════════════════════
// MassTransit registration (Phase 1 prerequisite)
// ══════════════════════════════════════════
// Registers RabbitMQ consumer and publisher configuration.
// This enables asynchronous communication between modules via domain events.
//TODO: builder.Services.AddMassTransitWithAssemblies(builder.Configuration, catalogAssembly, basketAssembly, orderingAssembly);


// ══════════════════════════════════════════
// Keycloak authentication (Phase 1 prerequisite)
// ══════════════════════════════════════════
// Adds JWT token validation and Authorization middleware support.
//TODO: builder.Services.AddKeycloakWebApiAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// ══════════════════════════════════════════
// Per-module service registration (Phase 1 prerequisite)
// ══════════════════════════════════════════
// Each module (Catalog, Basket, Ordering) registers its own:
// - DbContext (Entity Framework)
// - Domain services
// - Application services
// This defers implementation details to each module layer.
builder.Services
    .AddCatalogModule(builder.Configuration)
    .AddBasketModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration);

// CustomExceptionHandler is the central exception handler (Shared.Exceptions.Handler namespace).
// It catches all exceptions thrown by endpoints or handlers and returns standardized ProblemDetails.
// This is added AFTER module services so modules can throw application-specific exceptions
// that the handler will catch and transform.
// The handler uses BadRequestException and InternalServerException to categorize errors.
builder.Services
    .AddExceptionHandler<CustomExceptionHandler>();


// ══════════════════════════════════════════
// Build the app and configure the HTTP pipeline
// ══════════════════════════════════════════
var app = builder.Build();

/ Configure the HTTP request pipeline.

// ══════════════════════════════════════════
// Carter endpoint mapping (Phase 1)
// ══════════════════════════════════════════
// Maps all ICarterModule endpoints to the request pipeline.
// This must happen before exception handler middleware so exceptions are caught.
app.MapCarter();

// ══════════════════════════════════════════
// Structured request logging (Phase 1)
// ══════════════════════════════════════════
// Logs every HTTP request with rich context (method, path, status code, duration).
app.UseSerilogRequestLogging();

// ══════════════════════════════════════════
// Register CustomExceptionHandler in middleware pipeline
// ══════════════════════════════════════════
// app.UseExceptionHandler() activates the CustomExceptionHandler registered above.
// This middleware wraps all downstream middleware and endpoints, catching unhandled exceptions
// and transforming them into ProblemDetails JSON responses.
// It must come AFTER routing (MapCarter) but BEFORE authentication so that authentication
// failures (e.g., missing tokens) also return ProblemDetails.
// The handler reads the exception type and returns:
// - HTTP 400 + ProblemDetails for BadRequestException
// - HTTP 500 + ProblemDetails for InternalServerException (or any other exception)
app.UseExceptionHandler(options => { });

// ══════════════════════════════════════════
// Authentication and Authorization (Phase 1)
// ══════════════════════════════════════════
// UseAuthentication validates JWT tokens from Keycloak.
// UseAuthorization enforces [Authorize] attributes on endpoints.
app.UseAuthentication();
app.UseAuthorization();

// ══════════════════════════════════════════
// Per-module middleware setup (Phase 1)
// ══════════════════════════════════════════
// Each module's UseXxxModule() registers its own middleware if needed (e.g., custom context).
app
    .UseCatalogModule()
    .UseBasketModule()
    .UseOrderingModule();

// ══════════════════════════════════════════
// Start the application
// ══════════════════════════════════════════
app.Run();
