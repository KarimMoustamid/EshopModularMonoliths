
/// <summary>
/// This file defines the two-level entity contract used throughout the domain model.
/// 
/// IEntity is a marker interface that all domain entities must implement. It enforces
/// audit metadata tracking (CreatedAt, CreatedBy, LastModified, LastModifiedBy) at the
/// DDD boundary. This ensures that every entity in the system carries provenance and
/// change history, which is essential for event sourcing, audit logs, and domain events.
/// 
/// IEntity&lt;T&gt; extends IEntity and adds a strongly-typed Id property. This generic
/// interface allows each entity type to define its own identity type (Guid, int, string, etc.)
/// while still being recognized as an entity by the infrastructure layer (e.g., DbContext,
/// repositories, event publishers).
/// 
/// Design intent: These interfaces are thin enough that they do not impose behavior,
/// only structure. They act as a contract that the infrastructure layer relies on to:
/// - Identify what should be tracked as a domain object
/// - Extract identity for change detection and event correlation
/// - Ensure audit fields are populated consistently
/// 
/// Why two interfaces instead of one: IEntity provides the audit contract common to all
/// entities. IEntity&lt;T&gt; layers identity on top, allowing concrete classes to choose
/// their identity type. This separation enables entities to be recognized at the boundary
/// (IEntity) while allowing type-safe access to identity (IEntity&lt;T&gt;).
/// </summary>

namespace src.Shared.DDD
{
    /// <summary>
    /// Generic entity contract with strongly-typed identity.
    /// 
    /// Every aggregate root and value object container must implement this interface.
    /// T is the type of the entity's unique identifier (e.g., Guid, int, string).
    /// </summary>
    public interface IEntity<T> : IEntity
    {
        /// <summary>
        /// The unique identifier for this entity within its aggregate.
        /// Must be immutable after initial assignment in domain logic.
        /// The infrastructure layer reads this to track changes and correlate events.
        /// </summary>
        public T Id { get; set; }

    }

    /// <summary>
    /// Base entity contract for audit and identity tracking.
    /// 
    /// Every entity in the domain must implement this interface.
    /// It defines the minimal audit metadata that the system requires to track
    /// who created or modified a domain object and when.
    /// 
    /// These fields are populated by the infrastructure layer (e.g., DbContext interceptors,
    /// update triggers) and are not set directly by domain logic. They exist to satisfy
    /// compliance and debugging requirements, not to drive domain behavior.
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// The UTC timestamp when this entity was first created.
        /// Set by the infrastructure layer on insert; never updated afterward.
        /// Null if the entity has not yet been persisted.
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// The user or system identity that created this entity.
        /// This is captured from the request context (HttpContext.User.Identity.Name,
        /// or a service principal identifier). Used for audit trails and debugging.
        /// Null if the entity has not yet been persisted or if identity was unavailable.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// The UTC timestamp of the most recent modification to this entity.
        /// Updated by the infrastructure layer whenever any field changes.
        /// Null if the entity has never been modified since creation.
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// The user or system identity that last modified this entity.
        /// Updated by the infrastructure layer whenever any field changes.
        /// Null if the entity has never been modified since creation.
        /// </summary>
        public string? LastModifiedBy { get; set; }
    }
}