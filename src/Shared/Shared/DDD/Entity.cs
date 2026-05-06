using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using src.Shared.DDD;

namespace Shared.DDD
{
    /// <summary>
    /// Abstract base class for all domain entities.
    /// Provides generic Id management and domain event tracking.
    /// Every aggregate root and entity in the domain must inherit from this class.
    /// This ensures consistent identity handling and event sourcing across the domain.
    /// </summary>
    /// <typeparam name="T">The type of the entity's Id (e.g., int, Guid, string). Allows flexibility in identity strategy.</typeparam>
    public abstract class Entity<T> : IEntity
    {

        /// <summary>
        /// The unique identifier for this entity.
        /// Protected setter ensures only the entity itself (or a derived class) can assign the Id,
        /// typically during construction or by an aggregate root. This protects the invariant that an entity's Id cannot change after creation.
        /// </summary>
        public T Id { get; protected set; } = default!;

        /// <summary>
        /// Timestamp when this entity was first created in the system.
        /// Populated by the infrastructure layer (e.g., by the database or a middleware handler).
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// The user or system that created this entity.
        /// Helps with auditing and understanding the origin of domain state.
        /// Populated by the infrastructure layer, often from claims in an HTTP request.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Timestamp of the most recent modification to this entity.
        /// Null if the entity has never been modified after creation.
        /// Maintained by the infrastructure layer (e.g., interceptors in EF Core).
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// The user or system that last modified this entity.
        /// Helps with auditing and understanding who drove the most recent change.
        /// Populated by the infrastructure layer.
        /// </summary>
        public string? LastModifiedBy { get; set; }

        // ══════════════════════════════════════════
        // Domain Event Tracking
        // ══════════════════════════════════════════

        /// <summary>
        /// Internal collection of domain events that have been raised by this entity.
        /// These events represent state changes that are significant to the domain and must be communicated to other aggregates or bounded contexts.
        /// Private to prevent external code from directly manipulating the collection — only this class and derived classes can raise events via AddDomainEvent().
        /// </summary>
        private readonly List<IDomainEvent> _domainEvents = new();

        /// <summary>
        /// Retrieves all domain events that have been raised by this entity since the last clear.
        /// Returns a read-only view to prevent external code from modifying the collection.
        /// The infrastructure layer (typically a Unit of Work or repository) will read these events, publish them to message buses or handlers, and then clear them.
        /// </summary>
        /// <returns>A read-only list of all domain events raised by this entity.</returns>
        public IReadOnlyCollection<IDomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

        /// <summary>
        /// Clears all domain events from this entity.
        /// Called by the infrastructure layer after events have been successfully published to ensure events are not published twice.
        /// Should only be called after confirmed successful publishing — if cleared prematurely, events may be lost.
        /// </summary>
        public void ClearDomainEvents() => _domainEvents.Clear();

        /// <summary>
        /// Raises a domain event within this entity.
        /// Protected method so that only this entity and its derived classes can raise events — external code cannot inject arbitrary events.
        /// Events are accumulated and will be published by the infrastructure layer (Unit of Work, Application Service handlers).
        /// This is the mechanism by which entities signal important state changes to the rest of the system without tight coupling.
        /// </summary>
        /// <param name="domainEvent">The domain event to raise. Should be a record or immutable type that captures what happened and any relevant data.</param>
        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }


        // ══════════════════════════════════════════
        // STEP 11 — Inheritance Validation
        // ══════════════════════════════════════════
        // At this step, a temporary test class will inherit from Entity<T> and verify that:
        // - The Id property (inherited) can be read and written via the protected setter
        // - All three event methods (GetDomainEvents, ClearDomainEvents, AddDomainEvent) are accessible
        // - No compilation errors occur
        // This validates that the base class is properly structured for inheritance.
        // The test class will be deleted after verification — this comment serves as the checkpoint reminder.

        // ══════════════════════════════════════════
        // STEP 15 — Documentation Complete
        // ══════════════════════════════════════════
        // XML documentation comments have been added above to explain:
        // - The purpose of Entity<T> and why it exists (generic identity + event tracking)
        // - Why each property and method exists and the design intent behind it
        // - The invariants it protects (e.g., Id cannot be changed, events are read-only from outside)
        // - How the infrastructure layer depends on and uses this class
        // These comments make it clear that this is not just a data container but a carefully designed boundary
        // that enforces domain invariants and enables event-driven architecture.
    }
}