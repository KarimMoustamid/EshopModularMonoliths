// This file declares global using statements that eliminate the need for per-file imports
// across the entire Bootstrapper project. It serves as the composition root's wiring manifest.
// 
// Key principle: by declaring module namespaces globally here, we expose only the public
// extension method surface (e.g., services.AddCatalogModule()) without creating direct
// dependencies on module internals. This is the dependency injection seam that holds the
// modular monolith together.

// === MODULE NAMESPACES ===
// These three global usings make each module's extension methods visible to Program.cs
// without explicit imports. Each module (Catalog, Basket, Ordering) provides an AddXxxModule()
// extension method that registers its own internal services and handlers.
global using Catalog;
global using Basket;
global using Ordering;

// === FRAMEWORK & ROUTING ===
// Carter is the minimal routing library we use. Making it global allows Program.cs and
// any bootstrap code to reference Carter types (like ICarterModule) without imports.
global using Carter;

// === SHARED INFRASTRUCTURE ===
// Extension methods live in Shared.Extensions; making this global allows us to call
// shared composition methods (e.g., services.AddSharedServices()) from Program.cs.
global using Shared.Extensions;

// Exception handling middleware and models are globally available, so exception handler
// registration in Program.cs doesn't require explicit imports.
global using Shared.Exceptions.Handler;

// Serilog is our cross-cutting logging concern. Making it global means any code in the
// bootstrapper that needs structured logging (e.g., in configuration setup) doesn't need
// to re-import Serilog.
global using Serilog;

// === VALIDATION & CROSS-CUTTING CONCERNS ===
// FluentValidation is registered and used as a pipeline behavior. Global access means
// validation-related middleware and config in Program.cs is readable without imports.
global using FluentValidation;

// Shared.Behaviors contains the MediatR pipeline behaviors (validation, logging, error handling).
// Making it global allows Program.cs to wire them into the MediatR pipeline without imports.
global using Shared.Behaviors;

// === MESSAGING & INTEGRATION ===
// Shared.Messaging.Extensions contains the extension methods that wire up message publishing
// and event handling infrastructure (e.g., services.AddMessaging()). Global access is necessary
// because message publishing is used across all modules.
global using Shared.Messaging.Extensions;

// === AUTHENTICATION & SECURITY ===
// Keycloak.AuthServices.Authentication provides the extensions and types needed to configure
// OAuth2/OpenID Connect with our Keycloak instance in Program.cs. Making it global means
// auth configuration reads naturally without import noise.
global using Keycloak.AuthServices.Authentication;