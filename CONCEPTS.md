# CONCEPTS.md

> Architectural and design reference for the EShop Modular Monolith. Read top-to-bottom for progressive understanding, or jump to any section independently. Every section answers: what it is, the problem it solves, how it is implemented here, and when not to use it.

---

## Table of Contents

1. [Shared Kernel — DDD Base Classes](#1-shared-kernel--ddd-base-classes)
2. [Modular Monolith Architecture](#2-modular-monolith-architecture)
3. [Vertical Slice Architecture](#3-vertical-slice-architecture)
4. [CQRS with MediatR](#4-cqrs-with-mediatr)
5. [MediatR Pipeline Behaviors](#5-mediatr-pipeline-behaviors)
6. [Domain-Driven Design — Aggregates and Entities](#6-domain-driven-design--aggregates-and-entities)
7. [Domain-Driven Design — Value Objects](#7-domain-driven-design--value-objects)
8. [Domain Events and In-Process Dispatch](#8-domain-events-and-in-process-dispatch)
9. [EF Core Interceptors](#9-ef-core-interceptors)
10. [Module Registration Pattern](#10-module-registration-pattern)
11. [Carter — Minimal API Endpoint Organization](#11-carter--minimal-api-endpoint-organization)
12. [Repository Pattern](#12-repository-pattern)
13. [Decorator Pattern — Cached Repository](#13-decorator-pattern--cached-repository)
14. [Cache-Aside Pattern](#14-cache-aside-pattern)
15. [In-Process Cross-Module Communication via Contracts](#15-in-process-cross-module-communication-via-contracts)
16. [Integration Events and Async Cross-Module Communication](#16-integration-events-and-async-cross-module-communication)
17. [Outbox Pattern for Reliable Messaging](#17-outbox-pattern-for-reliable-messaging)
18. [Global Exception Handling](#18-global-exception-handling)
19. [Keycloak Authentication and Authorization](#19-keycloak-authentication-and-authorization)
20. [Structured Logging with Serilog](#20-structured-logging-with-serilog)
21. [Pagination](#21-pagination)
22. [Data Seeding](#22-data-seeding)

---

## 1. Shared Kernel — DDD Base Classes

### What It Is

The Shared Kernel is a small set of base types that every module inherits from. It defines the identity, audit trail, and domain event contracts that all domain objects conform to. It lives in `src/Shared/Shared/DDD/` and `src/Shared/Shared.Contracts/`.

### The Problem It Solves

Without a shared kernel, each module would independently invent its own `BaseEntity`, its own audit fields, and its own event dispatch mechanism — with subtly different semantics. Consistency bugs emerge at the seams: one module uses `int` IDs, another uses `Guid`; one dispatches events before save, another after. The shared kernel eliminates this class of inconsistency by making the contract a compile-time constraint.

### How It Is Implemented Here

The hierarchy is:

```
IEntity           ← CreatedAt, CreatedBy, LastModified, LastModifiedBy, Id
IAggregate        ← IEntity + DomainEvents collection + ClearDomainEvents()
Entity<T>         ← abstract class implementing IEntity<T>
Aggregate<TId>    ← abstract class extending Entity<TId>, implementing IAggregate<TId>
```

`IEntity` in `Shared/DDD/IEntity.cs`:
```csharp
public interface IEntity
{
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModified { get; set; }
    public string? LastModifiedBy { get; set; }
}
```

`Aggregate<TId>` in `Shared/DDD/Aggregate.cs`:
```csharp
public abstract class Aggregate<TId> : Entity<TId>, IAggregate<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public IDomainEvent[] ClearDomainEvents()
    {
        IDomainEvent[] dequeuedEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return dequeuedEvents;
    }
}
```

Every domain root in the system extends this: `Product : Aggregate<Guid>`, `ShoppingCart : Aggregate<Guid>`, `Order : Aggregate<Guid>`. `OrderItem` and `ShoppingCartItem` extend `Entity<Guid>` directly — they are not roots and cannot raise events independently.

`IDomainEvent` in `Shared/DDD/IDomainEvent.cs` extends MediatR's `INotification`, which is what allows MediatR to dispatch domain events as notifications:
```csharp
public interface IDomainEvent : INotification
{
    Guid EventId => Guid.NewGuid();
    public DateTime OccurredOn => DateTime.Now;
    public string EventType => GetType().AssemblyQualifiedName!;
}
```

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Consistent identity and audit across all modules | All modules take a hard compile-time dependency on `Shared` |
| Domain event dispatch is automatic for any aggregate | The `IEntity` audit fields are set to a hardcoded `"mehmet"` string — not real user context |
| Eliminates boilerplate in every module | `ClearDomainEvents` mutates state — not thread-safe if aggregates are shared across threads |

Do not put business logic in the shared kernel. The moment you add a rule that is specific to one module's aggregate into `Aggregate<TId>`, you have made the kernel load-bearing for business behaviour — and you lose the ability to evolve modules independently.

---

## 2. Modular Monolith Architecture

### What It Is

A Modular Monolith is a single deployable process whose internal structure enforces the same isolation boundaries that a microservices architecture would enforce across network boundaries — but without the operational complexity of distributed systems. The key constraint: modules share a process and a binary but must not share data stores or reference each other's implementation assemblies directly.

### The Problem It Solves

A classic layered monolith (Controllers → Services → Repositories) has no enforced internal boundaries. Any class can call any other class. Technical debt accumulates as "big ball of mud" — every change risks regressions everywhere. Microservices solve isolation but introduce network latency, distributed transactions, complex deployment, and service discovery overhead. A Modular Monolith captures the isolation benefit at a lower operational cost, and is a safer evolutionary path: individual modules can be extracted to microservices later (the Strangler Fig pattern mentioned in the README) without rewriting the business logic.

### How It Is Implemented Here

The single entry point is `src/Bootstrapper/Api/Program.cs`. It references the module assemblies and registers them:

```csharp
builder.Services
    .AddCatalogModule(builder.Configuration)
    .AddBasketModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration);

app
    .UseCatalogModule()
    .UseBasketModule()
    .UseOrderingModule();
```

Each module has its own:

**Isolated DbContext with its own PostgreSQL schema:**
```csharp
// CatalogDbContext.cs
builder.HasDefaultSchema("catalog");

// BasketDbContext.cs
builder.HasDefaultSchema("basket");

// OrderingDbContext.cs
builder.HasDefaultSchema("ordering");
```

All three contexts point at the same PostgreSQL instance (`EShopDb` database) but write to separate schemas. No module's EF context can see another module's tables.

**Dedicated module registration methods** (`CatalogModule.cs`, `BasketModule.cs`, `OrderingModule.cs`) that register all services, DbContext, interceptors, and middleware for that module only.

**Compile-time boundary enforcement via project references:** The `Basket` module does not reference `Catalog`'s implementation assembly. It only references `Catalog.Contracts`. The `Ordering` module references neither — it communicates only via messaging events from `Shared.Messaging`.

```
Basket.csproj  →  Catalog.Contracts.csproj  (allowed)
Basket.csproj  →  Catalog.csproj            (NOT referenced)
```

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Single deployment unit — no Docker orchestration per service | All modules share the same process memory and CPU — one runaway module affects all |
| Strong isolation without network overhead | Shared database instance is still a deployment coupling point |
| Easy local development — one `docker-compose up` | Schema migrations must be coordinated — a broken migration in one module fails the whole startup |
| Natural migration path to microservices | Schema isolation requires discipline — a developer can still write raw SQL that crosses schema boundaries |

Do not use this architecture if teams are large enough that independent deployment velocity is the primary constraint. At that scale, microservices' operational cost is worth the team autonomy.

---

## 3. Vertical Slice Architecture

### What It Is

Vertical Slice Architecture (VSA) organizes code by **feature** rather than by **layer**. Instead of horizontal folders (`Controllers/`, `Services/`, `Repositories/`) that group code by technical role, each feature gets its own folder containing everything it needs from HTTP endpoint to database query — its own vertical slice through all layers.

### The Problem It Solves

Layer-based organization creates a false coupling: all controllers are co-located even when they serve unrelated features, all services are co-located even when they have no shared logic. A change to the "Create Product" feature touches `ProductsController`, `ProductService`, `ProductRepository`, and `ProductDto` — four separate folders. With VSA, the entire feature is co-located, and a change to "Create Product" touches only `CreateProduct/`. Features that don't touch each other's folders cannot accidentally break each other.

### How It Is Implemented Here

The Catalog module's feature structure:

```
Catalog/Products/Features/
├── CreateProduct/
│   ├── CreateProductHandler.cs   ← command record + validator + handler class in one file
│   └── CreateProductEndpoint.cs  ← Carter endpoint class
├── UpdateProduct/
│   ├── UpdateProductHandler.cs
│   └── UpdateProductEndpoint.cs
├── DeleteProduct/
│   ├── DeleteProductHandler.cs
│   └── DeleteProductEndpoint.cs
├── GetProducts/
│   ├── GetProductsHandler.cs
│   └── GetProductsEndpoint.cs
├── GetProductById/
│   ├── GetProductByIdHandler.cs
│   └── GetProductByIdEndpoint.cs
└── GetProductByCategory/
    ├── GetProductByCategoryHandler.cs
    └── GetProductByCategoryEndpoint.cs
```

A complete feature in one file — `CreateProductHandler.cs` — contains:
```csharp
// The command (input shape)
public record CreateProductCommand(ProductDto Product)
    : ICommand<CreateProductResult>;

// The result (output shape)
public record CreateProductResult(Guid Id);

// The validator (input rules)
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Product.Name).NotEmpty().WithMessage("Name is required");
        RuleFor(x => x.Product.Price).GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}

// The handler (business logic)
internal class CreateProductHandler(CatalogDbContext dbContext)
    : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = Product.Create(...);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreateProductResult(product.Id);
    }
}
```

All four concepts — command, result, validator, handler — live in one file because they change together. When the "Create Product" feature changes, exactly one file changes.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Feature-local changes: one feature change = one folder changes | Shared logic (e.g. a common query helper used by three features) has no obvious home |
| New features are additive — no existing files touched | Can produce duplication if developers copy-paste logic between feature slices instead of extracting |
| Parallel team development: two developers work on two features without touching the same files | Single-file containing command + validator + handler is unusual — developers unfamiliar with VSA find it disorienting |

Do not use VSA as an excuse to avoid abstractions. If three features share real domain logic, extract it to a shared domain service. VSA governs file organization, not whether shared code should exist.

---

## 4. CQRS with MediatR

### What It Is

Command Query Responsibility Segregation (CQRS) separates every operation into one of two kinds: a **Command** (mutates state, returns a result confirming the mutation) or a **Query** (reads state, returns data, causes no side effects). MediatR is the in-process message bus that dispatches both.

### The Problem It Solves

In a traditional service class, `ProductService` has both `CreateProduct()` and `GetProducts()` — read and write operations mixed in one object. As the codebase grows, the service class bloats, read and write concerns tangle, and optimizing reads (e.g. adding caching or `AsNoTracking`) risks breaking writes. CQRS makes the distinction explicit and structural: there is a different class for each command and each query, with no shared mutable state.

### How It Is Implemented Here

The CQRS marker interfaces live in `Shared.Contracts/CQRS/`:

```csharp
// ICommand.cs — a command that returns TResponse
public interface ICommand<out TResponse> : IRequest<TResponse> { }

// IQuery.cs — a query that returns T (read-only)
public interface IQuery<out T> : IRequest<T> where T : notnull { }

// ICommandHandler.cs — handles a specific command
public interface ICommandHandler<in TCommand, TResponse>
    : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull { }

// IQueryHandler.cs — handles a specific query
public interface IQueryHandler<in TQuery, TResponse>
    : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull { }
```

These are thin wrappers over MediatR's `IRequest<T>` and `IRequestHandler<,>`. Their purpose is to make intent visible at the type level and to allow behaviors (see [Section 5](#5-mediatr-pipeline-behaviors)) to target only commands or only queries.

An example command and its handler from `Catalog/Products/Features/CreateProduct/CreateProductHandler.cs`:

```csharp
public record CreateProductCommand(ProductDto Product)
    : ICommand<CreateProductResult>;

internal class CreateProductHandler(CatalogDbContext dbContext)
    : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(
        CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = CreateNewProduct(command.Product);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreateProductResult(product.Id);
    }
}
```

An example query from `Catalog/Products/Features/GetProducts/GetProductsHandler.cs`:

```csharp
public record GetProductsQuery(PaginationRequest PaginationRequest)
    : IQuery<GetProductsResult>;

internal class GetProductsHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetProductsQuery, GetProductsResult>
{
    public async Task<GetProductsResult> Handle(
        GetProductsQuery query, CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .AsNoTracking()   // safe because this is a query — no mutation follows
            .OrderBy(p => p.Name)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        ...
    }
}
```

`AsNoTracking()` appears on every query handler and never on command handlers — because commands need EF's change tracking to detect and persist mutations. This distinction is made safe by the CQRS type system.

MediatR is registered centrally in `Shared/Extensions/MediatRExtentions.cs`:

```csharp
services.AddMediatR(config =>
{
    config.RegisterServicesFromAssemblies(assemblies);
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
});
```

All three module assemblies are passed so MediatR discovers every handler across all modules from one registration call.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Clear separation of read and write models | More classes and files for simple CRUD — a `GetProductById` query is a full class just to call `FindAsync` |
| Enables independent optimization of reads (caching, projections) and writes (validation, events) | MediatR dispatch adds one layer of indirection — harder to trace call stacks for newcomers |
| Pipeline behaviors apply selectively to commands vs queries | Over-engineering for small applications where reads and writes share the same simple data model |

Do not use CQRS if your read and write models are identical and you have no need to optimize them independently. The discipline it imposes costs more than it saves in simple CRUD applications.

---

## 5. MediatR Pipeline Behaviors

### What It Is

A MediatR Pipeline Behavior is middleware that wraps every command or query dispatch. Behaviors form a chain: each behavior calls `next()` to pass control to the next behavior and ultimately to the handler. This is the same concept as ASP.NET Core middleware, applied to the in-process MediatR pipeline rather than the HTTP pipeline.

### The Problem It Solves

Without pipeline behaviors, every handler would need to independently call validation, write log entries, and measure execution time. That is cross-cutting concern repetition — the same validation invocation code in 20 handler classes. Pipeline behaviors centralize cross-cutting concerns so handlers contain only business logic.

### How It Is Implemented Here

Two behaviors are registered globally for all modules in `Shared/Extensions/MediatRExtentions.cs`:

```csharp
config.AddOpenBehavior(typeof(ValidationBehavior<,>));
config.AddOpenBehavior(typeof(LoggingBehavior<,>));
```

**`ValidationBehavior<TRequest, TResponse>`** in `Shared/Behaviors/ValidationBehavior.cs`:

```csharp
public class ValidationBehavior<TRequest, TResponse>
    (IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>   // ← only applies to commands, not queries
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var validationResults =
            await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => r.Errors.Any())
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Any())
            throw new ValidationException(failures);

        return await next();
    }
}
```

The `where TRequest : ICommand<TResponse>` constraint is the key design decision: validation runs only for commands. Queries are structurally excluded. This is intentional — queries are read operations driven by URL parameters that are already constrained by routing; they don't carry mutable payloads that need FluentValidation rules.

Each command that needs validation registers its own `AbstractValidator<TCommand>` in the same file as the handler. FluentValidation's DI registration (`AddValidatorsFromAssemblies`) discovers and injects them automatically. For example, `CreateProductCommandValidator` in `CreateProductHandler.cs`:

```csharp
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Product.Name).NotEmpty().WithMessage("Name is required");
        RuleFor(x => x.Product.Category).NotEmpty().WithMessage("Category is required");
        RuleFor(x => x.Product.Price).GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}
```

**`LoggingBehavior<TRequest, TResponse>`** in `Shared/Behaviors/LoggingBehavior.cs`:

```csharp
public class LoggingBehavior<TRequest, TResponse>
    (ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>  // ← applies to ALL requests
{
    public async Task<TResponse> Handle(...)
    {
        logger.LogInformation("[START] Handle request={Request}...", typeof(TRequest).Name, ...);

        var timer = new Stopwatch();
        timer.Start();
        var response = await next();
        timer.Stop();

        if (timeTaken.Seconds > 3)
            logger.LogWarning("[PERFORMANCE] The request {Request} took {TimeTaken} seconds.", ...);

        logger.LogInformation("[END] Handled {Request} with {Response}", ...);
        return response;
    }
}
```

The pipeline execution order for a command is: `LoggingBehavior` → `ValidationBehavior` → `Handler`. If validation throws, `LoggingBehavior` still logs the `[END]` entry because the exception propagates up through it.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Validation and logging are zero-boilerplate for new handlers | Exceptions thrown inside behaviors bypass the normal `[END]` log line cleanly only if the behavior catches them — which these don't |
| Selective application (commands only for validation) is enforced by the type system | Order of behavior registration matters and is not obvious from reading individual behavior classes |
| A new cross-cutting concern (e.g. caching, retry) is one new behavior class | Open behaviors apply globally — there is no per-handler opt-out mechanism without type-level constraints |

Do not add behaviors that have per-handler configuration requirements. If a behavior needs to know whether a specific handler opts in or out, you need a different mechanism (e.g. marker interfaces or attributes per handler).

---

## 6. Domain-Driven Design — Aggregates and Entities

### What It Is

In DDD, an **Aggregate** is a cluster of domain objects treated as a single unit of consistency. One object within the cluster is the **Aggregate Root** — the only entry point through which the cluster's state can be changed. All invariants of the cluster are enforced by the root. An **Entity** is a domain object with a unique identity that persists over time, but which is not a root and cannot be accessed directly from outside its aggregate.

### The Problem It Solves

Without aggregates, any code anywhere can reach into `_items` and add an `OrderItem` with a negative price or zero quantity — violating business invariants. Aggregates are the enforcement mechanism: all mutations go through the root's methods, which contain the validation logic. This is why `ShoppingCart._items` is a private field while `Items` is an `IReadOnlyList` — you cannot add items by reaching into the collection; you must call `ShoppingCart.AddItem()`.

### How It Is Implemented Here

**`ShoppingCart` aggregate** in `Basket/Basket/Models/ShoppingCart.cs`:

```csharp
public class ShoppingCart : Aggregate<Guid>
{
    public string UserName { get; private set; } = default!;

    private readonly List<ShoppingCartItem> _items = new();
    public IReadOnlyList<ShoppingCartItem> Items => _items.AsReadOnly();
    public decimal TotalPrice => Items.Sum(x => x.Price * x.Quantity);

    // Factory method — the only way to create a valid ShoppingCart
    public static ShoppingCart Create(Guid id, string userName)
    {
        ArgumentException.ThrowIfNullOrEmpty(userName);
        var shoppingCart = new ShoppingCart { Id = id, UserName = userName };
        return shoppingCart;
    }

    public void AddItem(Guid productId, int quantity, string color, decimal price, string productName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        var existingItem = Items.FirstOrDefault(x => x.ProductId == productId);
        if (existingItem != null)
            existingItem.Quantity += quantity;  // merges duplicate products
        else
            _items.Add(new ShoppingCartItem(Id, productId, quantity, color, price, productName));
    }
}
```

Key observations:
- Private constructor (implicit) — you cannot call `new ShoppingCart()` directly from outside. You must use `ShoppingCart.Create()`.
- `_items` is private. `Items` is `IReadOnlyList` — no external code can call `Items.Add()`.
- `TotalPrice` is a computed property — it is never stored, always derived from the current state of items.
- `AddItem` enforces the invariant that quantity and price must be positive before the item is added.

**`Order` aggregate** in `Ordering/Orders/Models/Order.cs` follows the identical pattern: private `_items`, `IReadOnlyList<OrderItem> Items`, factory method `Order.Create()`, and methods `Add()` / `Remove()`.

**`Product` aggregate** in `Catalog/Products/Models/Product.cs` raises domain events as part of its mutation methods:

```csharp
public static Product Create(...)
{
    var product = new Product { ... };
    product.AddDomainEvent(new ProductCreatedEvent(product));  // raises an event on creation
    return product;
}

public void Update(string name, ..., decimal price)
{
    // only raises the price-changed event if the price actually changed
    if (Price != price)
    {
        Price = price;
        AddDomainEvent(new ProductPriceChangedEvent(this));
    }
}
```

**`ShoppingCartItem`** in `Basket/Basket/Models/ShoppingCartItem.cs` extends `Entity<Guid>` — not `Aggregate<Guid>`. It has no domain events and cannot be accessed without going through `ShoppingCart`. Its constructor is `internal` to prevent creation from outside the module, and `Quantity` is `internal set` to allow the parent aggregate to increment it.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| All invariants are enforced in one place — the aggregate root | EF Core has friction with private constructors and private backing fields — requires custom JSON converters and EF configurations |
| Domain logic lives in the domain model, not scattered in handlers or services | Large aggregates become transaction bottlenecks — everything inside one aggregate is saved in one DB round trip |
| `IReadOnlyList` exposure prevents collection mutation from outside the aggregate | If you need to query items directly (e.g. `UpdateItemPriceInBasket`), you must bypass the aggregate and query `ShoppingCartItems` directly — which is done in `UpdateItemPriceInBasketHandler` |

Do not make an aggregate so large that it encompasses unrelated entities just to enforce a rule. If two concepts can change independently and don't share invariants, they should be separate aggregates.

---

## 7. Domain-Driven Design — Value Objects

### What It Is

A Value Object is a domain concept defined entirely by its attributes rather than by an identity. Two `Address` instances with the same field values are considered equal. Value objects are immutable — once created, they cannot be changed. If you need a different address, you create a new one.

### The Problem It Solves

Without value objects, `Address` becomes a mutable data bag that any code can modify. An `Order.ShippingAddress.FirstName = "x"` becomes possible from anywhere, bypassing validation. Value objects make illegal states unrepresentable: you cannot construct an `Address` without a valid `AddressLine` and `EmailAddress` because the factory method enforces it.

### How It Is Implemented Here

**`Address`** in `Ordering/Orders/ValueObjects/Address.cs`:

```csharp
public record Address
{
    public string FirstName { get; } = default!;
    // ... other properties with init-only getters

    protected Address() { }  // required by EF Core for materialization

    private Address(string firstName, ...) { ... }  // private constructor

    public static Address Of(string firstName, ..., string addressLine, ...)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressLine);

        return new Address(firstName, ...);
    }
}
```

**`Payment`** in `Ordering/Orders/ValueObjects/Payment.cs` follows the same pattern with CVV length validation:

```csharp
public static Payment Of(string cardName, string cardNumber, string expiration, string cvv, int paymentMethod)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
    ArgumentException.ThrowIfNullOrWhiteSpace(cardNumber);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(cvv.Length, 3);

    return new Payment(cardName, cardNumber, expiration, cvv, paymentMethod);
}
```

Both are C# `record` types, which provides structural equality automatically — two `Address` records with the same field values are `==` equal.

EF Core maps them using `ComplexProperty` in `Ordering/Data/Configurations/OrderConfiguration.cs`:

```csharp
builder.ComplexProperty(
   o => o.ShippingAddress, addressBuilder =>
   {
       addressBuilder.Property(a => a.FirstName).HasMaxLength(50).IsRequired();
       // ...
   });
```

`ComplexProperty` (EF Core 8) stores the value object's fields as columns on the owning table (`Orders`) with a naming convention like `ShippingAddress_FirstName`. No separate table or foreign key is created — the value object is part of the aggregate's row.

The `protected Address()` constructor exists exclusively for EF Core's object materialization. It must be `protected` rather than private because EF uses reflection to construct instances from database reads. Application code cannot call it because there is no public path to it.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Validation at construction time — invalid value objects cannot exist | EF Core requires a `protected` parameterless constructor, which slightly relaxes the immutability guarantee |
| Structural equality is automatic with C# `record` | `ComplexProperty` stores fields inline on the owner's table — the table gets wider as value objects grow |
| Self-documenting domain model — `Payment.Of(...)` reads as domain language | Cannot be queried independently via LINQ — you must go through the owning aggregate |

Do not use a value object for a concept that has meaningful identity over time (e.g. a customer account). If the thing has an ID and a lifecycle, it is an entity, not a value object.

---

## 8. Domain Events and In-Process Dispatch

### What It Is

A Domain Event is a record of something important that happened inside the domain. It is named in past tense because it represents a fact: `ProductCreatedEvent`, `ProductPriceChangedEvent`, `OrderCreatedEvent`.

### The Problem It Solves

Without domain events, the code that changes an aggregate also has to know every side effect caused by that change. For example, changing a product price might need to update baskets, publish an integration event, clear cache, or write an audit record. That makes the original handler too coupled.

Domain events let the aggregate say: "this happened." Other handlers decide what to do about it.

### How It Is Implemented Here

Aggregates collect domain events internally:

```csharp
public abstract class Aggregate<TId> : Entity<TId>, IAggregate<TId>
{
    private readonly List<IDomainEvent> domainEvents = new();

    public IReadOnlyList<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        domainEvents.Add(domainEvent);
    }

    public IDomainEvent[] ClearDomainEvents()
    {
        var events = domainEvents.ToArray();
        domainEvents.Clear();
        return events;
    }
}
```

An aggregate raises an event during a meaningful state transition:

```csharp
public void UpdatePrice(decimal price)
{
    if (Price == price)
        return;

    Price = price;
    AddDomainEvent(new ProductPriceChangedEvent(this));
}
```

The important design point: the aggregate does not publish the event directly. It only records it. Publishing is infrastructure's job.

Usually, an EF Core interceptor or unit-of-work layer collects domain events from changed aggregates and publishes them through MediatR:

```csharp
foreach (var domainEvent in aggregate.ClearDomainEvents())
{
    await mediator.Publish(domainEvent, cancellationToken);
}
```

Because `IDomainEvent` extends MediatR's `INotification`, many handlers can react to the same event.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Decouples the original command handler from downstream side effects | Execution order of handlers can become important and hard to see |
| Keeps domain language explicit: `ProductPriceChangedEvent` is meaningful | Too many tiny events can make the flow difficult to trace |
| Works naturally with MediatR notifications | In-process events disappear if the process crashes before dispatch |

Do not use domain events for communication that must survive process failure. For that, publish an integration event through an outbox.

---

## 9. EF Core Interceptors

### What It Is

EF Core interceptors are hooks that run during Entity Framework operations. They let you add behavior around database work without putting that behavior inside every repository or handler.

Common interceptor use cases:

- Set audit fields before saving.
- Collect and dispatch domain events.
- Log slow queries.
- Convert domain events into outbox messages.

### The Problem It Solves

Without interceptors, every command handler would need to remember to set `CreatedAt`, update `LastModified`, dispatch domain events, or write outbox records. That creates copy-paste infrastructure code and makes correctness depend on every developer remembering the same steps.

Interceptors centralize those rules at the persistence boundary.

### How It Is Implemented Here

The common registration shape is:

```csharp
services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

services.AddDbContext<CatalogDbContext>((sp, options) =>
{
    options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
    options.UseNpgsql(connectionString);
});
```

`AuditableEntityInterceptor` typically runs before save and updates audit fields:

```csharp
public override InterceptionResult<int> SavingChanges(
    DbContextEventData eventData,
    InterceptionResult<int> result)
{
    var entries = eventData.Context.ChangeTracker
        .Entries<IEntity>();

    foreach (var entry in entries)
    {
        if (entry.State == EntityState.Added)
            entry.Entity.CreatedAt = DateTime.UtcNow;

        if (entry.State == EntityState.Modified)
            entry.Entity.LastModified = DateTime.UtcNow;
    }

    return base.SavingChanges(eventData, result);
}
```

`DispatchDomainEventsInterceptor` typically runs around `SaveChangesAsync`, finds changed aggregates, extracts domain events, and publishes them through MediatR.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Cross-cutting persistence behavior is automatic | Hidden behavior: saving an entity can do more than the handler visibly says |
| Keeps handlers focused on business logic | Interceptor order can matter |
| Consistent audit/domain event behavior across modules | Harder to unit test than explicit handler code |

Do not put business decisions in EF interceptors. They should handle infrastructure concerns, not decide whether an order is valid.

---

## 10. Module Registration Pattern

### What It Is

The Module Registration Pattern gives each module two extension methods:

```csharp
services.AddBasketModule(configuration);
app.UseBasketModule();
```

`Add...Module` registers services into dependency injection. `Use...Module` configures middleware or startup behavior after the app is built.

### The Problem It Solves

The API host should not know every internal class inside every module. If `Program.cs` manually registers `BasketDbContext`, `BasketRepository`, `CatalogDataSeeder`, and every endpoint, the host becomes tightly coupled to module internals.

Module registration keeps each module responsible for its own wiring.

### How It Is Implemented Here

The host calls module-level extension methods:

```csharp
builder.Services
    .AddCatalogModule(builder.Configuration)
    .AddBasketModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration);

app
    .UseCatalogModule()
    .UseBasketModule()
    .UseOrderingModule();
```

Inside a module:

```csharp
public static IServiceCollection AddCatalogModule(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Register Catalog services here.
    return services;
}

public static IApplicationBuilder UseCatalogModule(
    this IApplicationBuilder app)
{
    // Configure Catalog startup behavior here.
    return app;
}
```

This creates a clean boundary:

```text
Host knows: AddCatalogModule()
Catalog knows: CatalogDbContext, handlers, repositories, seeders
```

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Keeps host startup clean | Registration logic can become large if the module grows |
| Makes module ownership explicit | Startup order matters when modules depend on shared infrastructure |
| Helps future extraction to microservices | The host still references all module assemblies |

Do not use module registration as a dumping ground. Keep registration grouped by concern: endpoints, application services, infrastructure, background services.

---

## 11. Carter — Minimal API Endpoint Organization

### What It Is

Carter is a lightweight library for organizing ASP.NET Core Minimal API endpoints into module classes. A Carter module implements `ICarterModule` and maps routes inside `AddRoutes`.

### The Problem It Solves

Minimal APIs are clean at first, but `Program.cs` becomes crowded when every endpoint is mapped there:

```csharp
app.MapGet(...);
app.MapPost(...);
app.MapPut(...);
app.MapDelete(...);
```

Carter keeps endpoints close to the feature they belong to.

### How It Is Implemented Here

A Carter endpoint module looks like this:

```csharp
public sealed class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/products", async (CreateProductRequest request, ISender sender) =>
        {
            var command = new CreateProductCommand(request.Product);
            var result = await sender.Send(command);
            return Results.Created($"/products/{result.Id}", result);
        });
    }
}
```

Carter is registered through a shared extension:

```csharp
services.AddCarter(configurator: config =>
{
    foreach (var assembly in assemblies)
    {
        var modules = assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(ICarterModule)))
            .ToArray();

        config.WithModules(modules);
    }
});
```

This is reflection-based discovery:

```text
Scan assemblies.
Find classes implementing ICarterModule.
Register them with Carter.
```

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Keeps endpoints feature-local | Reflection discovery can feel magical to newcomers |
| Avoids a huge `Program.cs` | Requires Carter dependency in the shared registration project |
| Works naturally with vertical slices | Route conflicts are still possible if two modules map the same route |

Do not use Carter to hide business logic inside endpoints. Endpoints should translate HTTP to commands/queries, then delegate to MediatR.

---

## 12. Repository Pattern

### What It Is

The Repository Pattern wraps data access behind an interface. Application code asks for domain operations, not database details.

```csharp
public interface IBasketRepository
{
    Task<ShoppingCart?> GetBasket(string userName, CancellationToken cancellationToken);
    Task SaveBasket(ShoppingCart basket, CancellationToken cancellationToken);
}
```

### The Problem It Solves

Without a repository, handlers directly know how basket data is stored. If you move baskets from PostgreSQL to Redis, every handler that queries the basket storage may need to change.

A repository creates a stable application-facing contract.

### How It Is Implemented Here

The expected Basket shape is:

```csharp
services.AddScoped<IBasketRepository, BasketRepository>();
```

Handlers depend on the interface:

```csharp
internal sealed class GetBasketHandler(IBasketRepository repository)
{
    public async Task<GetBasketResult> Handle(GetBasketQuery query, CancellationToken ct)
    {
        var basket = await repository.GetBasket(query.UserName, ct);
        return new GetBasketResult(basket);
    }
}
```

The handler does not know whether the repository uses EF Core, Redis, Dapper, or an external service.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Isolates storage decisions from handlers | Can become a thin wrapper over EF Core with little value |
| Makes caching/decorators easier | Generic repositories often hide useful EF Core features |
| Improves testability of application logic | Too many repository methods can become another service layer |

Do not create a generic `IRepository<T>` by default. Prefer feature-specific repository contracts when the storage concern is meaningful, like `IBasketRepository`.

---

## 13. Decorator Pattern — Cached Repository

### What It Is

The Decorator Pattern wraps an object with another object that implements the same interface. The wrapper adds behavior before or after delegating to the original object.

### The Problem It Solves

Caching is not the core responsibility of a database repository. The repository should know how to load and save baskets. A cached decorator adds caching without changing the repository itself.

### How It Is Implemented Here

The registration shape is:

```csharp
services.AddScoped<IBasketRepository, BasketRepository>();
services.Decorate<IBasketRepository, CachedBasketRepository>();
```

The resolved dependency chain becomes:

```text
Handler
  -> IBasketRepository
     -> CachedBasketRepository
        -> BasketRepository
           -> Database
```

The decorator implements the same interface:

```csharp
public sealed class CachedBasketRepository : IBasketRepository
{
    private readonly IBasketRepository inner;
    private readonly IDistributedCache cache;

    public CachedBasketRepository(
        IBasketRepository inner,
        IDistributedCache cache)
    {
        this.inner = inner;
        this.cache = cache;
    }

    public async Task<ShoppingCart?> GetBasket(string userName, CancellationToken ct)
    {
        var cached = await cache.GetStringAsync(userName, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<ShoppingCart>(cached);

        var basket = await inner.GetBasket(userName, ct);

        if (basket is not null)
            await cache.SetStringAsync(userName, JsonSerializer.Serialize(basket), cancellationToken: ct);

        return basket;
    }
}
```

`services.Decorate` normally comes from Scrutor.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Adds caching without changing repository code | More indirection when debugging |
| Handler remains unaware of cache | Cache invalidation must be handled carefully |
| Multiple decorators can be stacked | Decorator order matters |

Do not use a decorator if the extra behavior is part of the core operation. Use it for cross-cutting behavior like caching, logging, retry, or metrics.

---

## 14. Cache-Aside Pattern

### What It Is

Cache-Aside is a caching pattern where the application manually checks the cache before loading from the database.

### The Problem It Solves

Database reads are slower and more expensive than cache reads. If basket data is requested often, repeatedly loading it from the database wastes time and resources.

### How It Is Implemented Here

The read flow:

```text
1. Check cache by key.
2. If value exists, return it.
3. If value is missing, load from database.
4. Store database result in cache.
5. Return result.
```

The write flow:

```text
1. Save new basket state to database.
2. Remove or update the cached basket.
```

Example:

```csharp
public async Task SaveBasket(ShoppingCart basket, CancellationToken ct)
{
    await inner.SaveBasket(basket, ct);
    await cache.RemoveAsync(GetCacheKey(basket.UserName), ct);
}
```

Removing the cache entry after save is often simpler than updating it. The next read reloads fresh data.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Simple and widely used | First read after expiration is still slow |
| Application controls what is cached | Risk of stale data if writes forget to invalidate cache |
| Works well for read-heavy data | Cache stampede can happen under high concurrency |

Do not cache data that changes constantly and must be perfectly fresh unless you have a strong invalidation strategy.

---

## 15. In-Process Cross-Module Communication via Contracts

### What It Is

In-process cross-module communication means one module asks another module for something inside the same running application, without HTTP or messaging. Contracts are used to prevent direct dependency on another module's implementation.

### The Problem It Solves

If Basket references Catalog directly, Basket can call internal Catalog classes and violate the module boundary. But Basket may still need product information. Contracts provide a narrow public API.

### How It Is Implemented Here

The allowed dependency shape:

```text
Basket -> Catalog.Contracts
Basket -X-> Catalog implementation
```

Catalog publishes contracts such as queries, DTOs, or integration-facing interfaces. Basket sends a query through MediatR:

```csharp
var result = await sender.Send(new GetProductByIdQuery(productId), cancellationToken);
```

Basket knows the contract, not the handler implementation. MediatR finds the handler at runtime because all module assemblies are registered.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Fast: no network call | Modules are still deployed together |
| Compile-time contracts are explicit | A contract change can still break consumers |
| Easier than async messaging for request/response needs | Can create hidden runtime coupling if overused |

Do not use in-process calls for business processes that must continue even if the target module fails. Use integration events for asynchronous workflows.

---

## 16. Integration Events and Async Cross-Module Communication

### What It Is

Integration Events are messages published between modules or services to announce something that happened. Unlike domain events, integration events are meant to cross boundaries.

### The Problem It Solves

Some workflows should not require the originating module to wait for every downstream action. For example, after an order is created, payment, inventory, notification, and analytics can react asynchronously.

### How It Is Implemented Here

A domain event is internal:

```csharp
public record ProductPriceChangedEvent(Product Product) : IDomainEvent;
```

An integration event is boundary-facing:

```csharp
public record ProductPriceChangedIntegrationEvent(
    Guid ProductId,
    decimal NewPrice,
    DateTime OccurredOn);
```

The usual flow:

```text
Aggregate changes.
Domain event is raised.
Domain event handler creates an integration event.
Integration event is written to outbox.
Background processor publishes it to the message broker.
Other modules consume it.
```

This separates internal domain shape from public event shape. Consumers do not receive the whole `Product` aggregate.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Decouples modules over time | Eventual consistency: consumers update later |
| Reliable with outbox pattern | Requires message contracts and versioning discipline |
| Good path toward microservices | Harder to trace than direct calls |

Do not use async events when the caller needs an immediate answer. Use a query/command contract for request-response.

---

## 17. Outbox Pattern for Reliable Messaging

### What It Is

The Outbox Pattern stores messages in the same database transaction as the business change. A background processor later reads the outbox table and publishes messages to a broker.

### The Problem It Solves

The dangerous sequence is:

```text
1. Save order to database.
2. Publish OrderCreated event.
```

If the app crashes after step 1 but before step 2, the database says the order exists but no message was published. The outbox fixes this by saving the message together with the order.

### How It Is Implemented Here

The command handler saves business state:

```csharp
dbContext.Orders.Add(order);
await dbContext.SaveChangesAsync(cancellationToken);
```

During the same save, infrastructure creates an outbox row:

```text
OutboxMessages
- Id
- Type
- Content
- OccurredOn
- ProcessedOn
- Error
```

A hosted service processes rows:

```csharp
public sealed class OutboxProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Poll unprocessed outbox rows.
        // Publish them.
        // Mark as processed.
    }
}
```

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Prevents lost messages after database commit | Requires polling or database notifications |
| Keeps business save and message persistence atomic | Messages may be published more than once |
| Makes async workflows reliable | Consumers must be idempotent |

Do not use the outbox for simple in-process side effects that do not need reliability across crashes.

---

## 18. Global Exception Handling

### What It Is

Global exception handling catches unhandled exceptions at the edge of the HTTP pipeline and converts them into consistent HTTP responses.

### The Problem It Solves

Without global handling, exceptions leak as inconsistent responses. One endpoint might return plain text, another JSON, another a stack trace in development.

### How It Is Implemented Here

The usual ASP.NET Core shape is:

```csharp
app.UseExceptionHandler();
```

or a custom handler:

```csharp
public sealed class CustomExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Server error",
            Detail = exception.Message
        };

        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
```

Validation exceptions can become `400 Bad Request` or `422 Unprocessable Entity`. Not-found exceptions can become `404`.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Consistent API error shape | Can hide useful debugging details if configured poorly |
| Keeps endpoints free of repetitive try/catch blocks | Must map exception types carefully |
| Prevents stack traces leaking in production | Does not replace domain-level validation |

Do not catch exceptions locally unless the endpoint can actually recover or return a more precise response.

---

## 19. Keycloak Authentication and Authorization

### What It Is

Keycloak is an identity provider. It handles login, users, roles, tokens, and authentication flows. The API validates JWT access tokens issued by Keycloak.

### The Problem It Solves

Authentication is security-critical and easy to get wrong. Keycloak lets the application delegate identity management to a dedicated system instead of building login, password reset, token signing, and role management manually.

### How It Is Implemented Here

The typical ASP.NET Core setup:

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

app.UseAuthentication();
app.UseAuthorization();
```

Endpoint protection:

```csharp
app.MapPost("/orders", ...)
   .RequireAuthorization();
```

Role-based authorization:

```csharp
app.MapDelete("/products/{id}", ...)
   .RequireAuthorization("AdminOnly");
```

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Mature identity provider | Another service to run and configure |
| Supports roles, realms, clients, JWTs | Token claims must be mapped carefully |
| Avoids homegrown auth | Local development setup is heavier |

Do not treat JWT validation as authorization by itself. Authentication proves who the user is; authorization decides what they can do.

---

## 20. Structured Logging with Serilog

### What It Is

Structured logging records log data as named properties instead of plain strings. Serilog is a logging library designed around this idea.

### The Problem It Solves

Plain logs are hard to query:

```text
User 123 created order 456
```

Structured logs preserve fields:

```csharp
logger.LogInformation(
    "User {UserId} created order {OrderId}",
    userId,
    orderId);
```

Now log tools can filter by `UserId` or `OrderId`.

### How It Is Implemented Here

The typical host setup:

```csharp
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});
```

Inside a MediatR behavior:

```csharp
logger.LogInformation(
    "Handling request {RequestName} {@Request}",
    typeof(TRequest).Name,
    request);
```

`{@Request}` destructures the object, capturing properties rather than only calling `ToString()`.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Easy filtering in log systems | Can accidentally log sensitive data |
| Great with correlation IDs | More storage than plain text logs |
| Works well with request/handler logging | Requires discipline around property names |

Do not log passwords, tokens, card numbers, or full personal data. Structured logs make sensitive data easier to search, which is powerful and dangerous.

---

## 21. Pagination

### What It Is

Pagination splits large result sets into pages. Instead of returning every product, the API returns a controlled slice.

### The Problem It Solves

Returning thousands of rows from one endpoint is slow, memory-heavy, and hard for clients to display. Pagination protects the database, API, and client.

### How It Is Implemented Here

A request object usually carries page information:

```csharp
public record PaginationRequest(int PageIndex = 0, int PageSize = 10);
```

The query handler applies it:

```csharp
var products = await dbContext.Products
    .AsNoTracking()
    .OrderBy(p => p.Name)
    .Skip(pageIndex * pageSize)
    .Take(pageSize)
    .ToListAsync(cancellationToken);
```

The response should include both items and paging metadata:

```csharp
public record PaginatedResult<T>(
    int PageIndex,
    int PageSize,
    long Count,
    IEnumerable<T> Data);
```

Always use a stable `OrderBy` before `Skip` and `Take`. Without ordering, the database is free to return rows in different orders between requests.

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Protects API from huge responses | Offset pagination gets slower for very deep pages |
| Gives clients predictable chunks | Data can shift between pages if rows are inserted/deleted |
| Easy to implement with EF Core | Requires a count query if total count is needed |

For very large datasets or infinite scrolling, consider cursor-based pagination instead of `Skip`/`Take`.

---

## 22. Data Seeding

### What It Is

Data seeding inserts initial or required data into the database. This can include product categories, sample products, admin users, or lookup values.

### The Problem It Solves

A fresh database is empty. Without seeding, developers must manually insert data before the app can be tested. Environments become inconsistent because everyone has slightly different local data.

### How It Is Implemented Here

A shared seeding contract usually looks like:

```csharp
public interface IDataSeeder
{
    Task SeedAllAsync(CancellationToken cancellationToken = default);
}
```

A module registers its seeder:

```csharp
services.AddScoped<IDataSeeder, CatalogDataSeeder>();
```

Startup code can run all seeders:

```csharp
using var scope = app.ApplicationServices.CreateScope();

var seeders = scope.ServiceProvider.GetServices<IDataSeeder>();

foreach (var seeder in seeders)
{
    await seeder.SeedAllAsync();
}
```

Seeders should be idempotent. Running them twice should not duplicate data:

```csharp
if (await dbContext.Products.AnyAsync(cancellationToken))
    return;

dbContext.Products.AddRange(products);
await dbContext.SaveChangesAsync(cancellationToken);
```

### Trade-offs and When NOT to Use It

| Advantage | Cost |
|---|---|
| Fresh environments become usable immediately | Bad seed data can hide real production edge cases |
| Local development is easier | Must avoid duplicate inserts |
| Useful for lookup/reference data | Production seeding requires careful permissions and review |

Do not use seeders as a replacement for migrations. Migrations create schema; seeders insert data.
