using System;
using System.Collections.Generic;

namespace reromanlee.EventAggregator
{
    /// <summary>
    /// Non-generic view of <see cref="EventListeners{TEvent}"/> so the bus can store groups of
    /// different event types in one dictionary and clear them uniformly on dispose.
    /// </summary>
    internal interface IEventListeners
    {
        /// <summary>Removes all listeners and empties the published snapshot.</summary>
        void Clear();
    }

    /// <summary>
    /// The listeners subscribed to a single event type, kept as a mutable list plus an immutable
    /// copy-on-write snapshot. Mutations (rare) rebuild the snapshot array; publishing (hot path)
    /// only reads the current snapshot, so it never locks and never allocates.
    /// </summary>
    /// <remarks>
    /// Mutating members are not thread-safe on their own and must be called under the owning
    /// bus's mutation lock. <see cref="Snapshot"/> may be read from any thread without locking.
    /// </remarks>
    /// <typeparam name="TEvent">The event type this group's listeners handle.</typeparam>
    internal sealed class EventListeners<TEvent> : IEventListeners
    {
        private readonly List<IEventListener<TEvent>> listeners = new();

        // volatile so a publish on another thread observes a fully constructed array,
        // never a torn or stale-null reference.
        private volatile IEventListener<TEvent>[] snapshot = Array.Empty<IEventListener<TEvent>>();

        /// <summary>
        /// The immutable listener set to dispatch to. Never null; safe to iterate without locking
        /// because a concurrent subscribe/unsubscribe replaces the array instead of mutating it.
        /// </summary>
        public IEventListener<TEvent>[] Snapshot => snapshot;

        /// <summary>Adds a listener unless it is already present. Returns false on a duplicate.</summary>
        public bool Add(IEventListener<TEvent> listener)
        {
            if (listeners.Contains(listener))
            {
                return false;
            }
            listeners.Add(listener);
            snapshot = listeners.ToArray();
            return true;
        }

        /// <summary>Removes a listener if present. Returns false when it was not subscribed.</summary>
        public bool Remove(IEventListener<TEvent> listener)
        {
            if (!listeners.Remove(listener))
            {
                return false;
            }
            snapshot = listeners.Count == 0
                ? Array.Empty<IEventListener<TEvent>>()
                : listeners.ToArray();
            return true;
        }

        public void Clear()
        {
            listeners.Clear();
            snapshot = Array.Empty<IEventListener<TEvent>>();
        }
    }
}
