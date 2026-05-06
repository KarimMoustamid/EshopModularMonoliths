// ========================================
// This interface defines the contract that ALL domain events must satisfy.
// Domain events represent something important that happened in the business domain
// (e.g., OrderCreated, PaymentProcessed, InventoryReserved).
// 
// Key design decisions:
// - Inherits from INotification (MediatR) so the mediator can discover and route events automatically
// - Provides default implementations for EventId, OccurredOn, and EventType as protected properties
// - Each event instance gets a unique EventId to track it across the system
// - OccurredOn captures the exact moment the event occurred (essential for event ordering and replay)
// - EventType returns the AssemblyQualifiedName so events can be serialized/deserialized and 
//   routed by type name when replaying or forwarding to external systems
//
// This is a marker interface — its value lies not in methods but in signaling "this is a domain event"
// and providing a common shape for all events. Without a common interface, event handlers must know
// about specific event types, which couples them. With this interface, a single generic handler can
// subscribe to *any* IDomainEvent and make routing decisions based on EventType.

using MediatR; // MediatR provides INotification — the pub/sub contract for in-process event handling

namespace src.Shared.DDD
{
    public interface IDomainEvent : INotification
    {
        // EventId — a unique identifier for this specific event instance
        // Why Guid.NewGuid()? So every event can be tracked, correlated, and deduplicated.
        // The default ensures that if a concrete event forgets to set it, a new one is generated.
        // This is safer than nullable or required, because domain events MUST be trackable.
        Guid EventId => Guid.NewGuid();

        // OccurredOn — the moment in time when this event happened
        // Why DateTime.Now? Domain events must record when the business action occurred, not when
        // the event was handled. This timestamp is essential for:
        // - Event ordering (if two events arrive out of order, the timestamp tells which really came first)
        // - Audit trails (know exactly when each action happened)
        // - Event replay (when re-running the event log, preserve the original order and timing)
        // Note: In production, this should typically be set by the domain object that raises the event,
        // not by the infrastructure. The default here is a safety net; aggregates should assign it explicitly.
        public DateTime OccurredOn => DateTime.Now;

        // EventType — the fully qualified name of the concrete event class
        // Why AssemblyQualifiedName? So the event can be serialized to a message (RabbitMQ, database)
        // and later deserialized without losing type information. When reading events from a queue or log,
        // the consumer needs to know which concrete class to instantiate. The name alone isn't enough
        // (two assemblies could have OrderCreated); AssemblyQualifiedName includes the version and culture.
        // This allows events to survive across deployment boundaries and be routed intelligently.
        public string EventType => GetType().AssemblyQualifiedName!;
    }
}