using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace reromanlee.EventAggregator
{
    /// <summary>
    /// Thread-safe <see cref="IEventBus"/> implementation optimized for a publish-heavy workload.
    /// </summary>
    /// <remarks>
    /// <para><b>Performance.</b> Each event type keeps an immutable snapshot of its listeners that is
    /// rebuilt only when a listener is added or removed, so <see cref="Publish{TEvent}"/> takes no locks
    /// and allocates no memory — safe to call every frame without GC pressure.</para>
    /// <para><b>Delivery semantics.</b> Events are delivered synchronously on the publishing thread, in
    /// subscription order. A publish dispatches to the listeners subscribed at the moment it starts;
    /// listeners added or removed mid-publish (by a handler or another thread) take effect for
    /// subsequent publishes only. A listener that throws is logged and skipped, and the remaining
    /// listeners still receive the event.</para>
    /// <para><b>Lifetime.</b> The bus holds strong references to listeners. A listener that is never
    /// unsubscribed is kept alive and keeps receiving events, so teardown code must always unsubscribe.
    /// Unsubscribing after the bus was disposed is a harmless no-op.</para>
    /// <para><b>Recursion.</b> In the editor and development builds, runaway recursive publishing
    /// (a handler for event A publishing B whose handler publishes A again, forever) is detected and
    /// reported as an error instead of crashing the player with an undebuggable stack overflow.
    /// The check is compiled out of release builds entirely.</para>
    /// </remarks>
    public sealed class EventBus : IEventBus, IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Nested publishes deeper than this are treated as an infinite publish loop. The counter is
        /// per thread and shared by all bus instances; the limit is generous enough that legitimate
        /// event chains never trip it.
        /// </summary>
        private const int MaxPublishDepth = 64;

        [ThreadStatic] private static int publishDepth;
#endif

        // Listener groups keyed by event type. A ConcurrentDictionary lets the publish path read
        // lock-free while Subscribe/Unsubscribe/Dispose mutate under mutationLock. Entries are only
        // ever added or cleared, never replaced, so a raced read always sees a valid group.
        private readonly ConcurrentDictionary<Type, IEventListeners> listenerTypes = new();

        // Serializes all mutations (Subscribe/Unsubscribe/Dispose). Never held while listeners run,
        // so handlers are free to publish, subscribe, or unsubscribe re-entrantly without deadlocking.
        private readonly object mutationLock = new();

        private volatile bool isDisposed;

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException">The bus has been disposed.</exception>
        public void Publish<TEvent>(TEvent eventData)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(EventBus));
            }

            // No subscribers for this event type (yet) is a normal situation, not an error:
            // the listening system may simply not be active. Deliberately a silent no-op.
            if (!listenerTypes.TryGetValue(typeof(TEvent), out IEventListeners group))
            {
                return;
            }

            IEventListener<TEvent>[] snapshot = ((EventListeners<TEvent>)group).Snapshot;
            if (snapshot.Length == 0)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (++publishDepth > MaxPublishDepth)
            {
                publishDepth--;
                throw new InvalidOperationException(
                    $"Publish depth exceeded {MaxPublishDepth} while publishing '{typeof(TEvent).Name}'. " +
                    "Listeners are most likely publishing events that recursively trigger each other.");
            }
            try
            {
#endif
                for (int i = 0; i < snapshot.Length; i++)
                {
                    try
                    {
                        snapshot[i].Handle(eventData);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"Listener '{snapshot[i].GetType().Name}' threw while handling '{typeof(TEvent).Name}':\n{exception}");
                    }
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            }
            finally
            {
                publishDepth--;
            }
#endif
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="listener"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The bus has been disposed.</exception>
        public void Subscribe<TEvent>(IEventListener<TEvent> listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(EventBus));
            }

            lock (mutationLock)
            {
                // Re-check under the lock so a listener cannot slip in after Dispose cleared everything.
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(EventBus));
                }

                EventListeners<TEvent> group;
                if (listenerTypes.TryGetValue(typeof(TEvent), out IEventListeners existing))
                {
                    group = (EventListeners<TEvent>)existing;
                }
                else
                {
                    group = new EventListeners<TEvent>();
                    listenerTypes[typeof(TEvent)] = group;
                }

                if (!group.Add(listener))
                {
                    // A duplicate subscription would make the listener handle every event twice.
                    Debug.LogWarning(
                        $"Listener '{listener.GetType().Name}' is already subscribed to '{typeof(TEvent).Name}'; duplicate subscription ignored.");
                }
            }
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="listener"/> is null.</exception>
        public void Unsubscribe<TEvent>(IEventListener<TEvent> listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            // Unsubscribing after disposal is a no-op rather than an error: teardown code
            // (OnDestroy/OnDisable) routinely runs after the bus's owner has disposed it.
            if (isDisposed)
            {
                return;
            }

            lock (mutationLock)
            {
                if (!isDisposed && listenerTypes.TryGetValue(typeof(TEvent), out IEventListeners group))
                {
                    ((EventListeners<TEvent>)group).Remove(listener);
                }
            }
        }

        /// <summary>
        /// Releases all listeners. After disposal, <see cref="Publish{TEvent}"/> and
        /// <see cref="Subscribe{TEvent}"/> throw <see cref="ObjectDisposedException"/>, while
        /// <see cref="Unsubscribe{TEvent}"/> becomes a no-op so teardown code stays safe.
        /// Disposing more than once is allowed.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            lock (mutationLock)
            {
                if (isDisposed)
                {
                    return;
                }
                isDisposed = true;

                // Empty every snapshot so a publish that already passed the disposed check
                // delivers to no one instead of to stale listeners.
                foreach (IEventListeners group in listenerTypes.Values)
                {
                    group.Clear();
                }
                listenerTypes.Clear();
            }
        }
    }
}