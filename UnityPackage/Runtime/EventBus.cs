using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace reromanlee.EventAggregator
{
    /// <summary>
    /// Thread-safe <see cref="IEventBus"/> implementation optimized for a publish-heavy workload.
    /// </summary>
    /// <remarks>
    /// <para><b>Performance.</b> <see cref="Publish{TEvent}"/> never locks and never allocates: it finds the
    /// event type's listeners with one array read and walks them directly. Subscribing and unsubscribing are
    /// amortized O(1), so even thousands of listeners joining in one frame cost linear time and memory.</para>
    /// <para><b>Delivery semantics.</b> Events are delivered synchronously on the publishing thread, in
    /// subscription order. Listeners added during a publish receive events from the next publish on.
    /// Listeners removed during a publish stop receiving immediately, including the rest of the publish in
    /// progress. A listener that throws is logged and skipped; the remaining listeners still receive the event.</para>
    /// <para><b>Lifetime.</b> The bus holds strong references to listeners, so teardown code should always
    /// unsubscribe. A listener that is a destroyed Unity object is skipped and unsubscribed automatically, with
    /// a warning in the editor and development builds. Unsubscribing after the bus was disposed is a harmless no-op.</para>
    /// <para><b>Recursion.</b> In the editor and development builds, runaway recursive publishing (a handler
    /// for event A publishing B whose handler publishes A again, forever) is reported as an error and the rest
    /// of that event chain is cancelled, instead of crashing with an undebuggable stack overflow or freezing on
    /// fan-out. The check is compiled out of release builds entirely.</para>
    /// <para><b>Debugging.</b> In the editor, every bus reports its publishes and deliveries to the
    /// Event Debugger window, where instances appear as "EventBus N" or the name given via
    /// <see cref="SetDebugName"/>. All of this instrumentation — including calls to
    /// <see cref="SetDebugName"/> — is compiled out of player builds and costs nothing there.</para>
    /// </remarks>
    public sealed class EventBus : IEventBus, IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Nested publishes deeper than this are treated as an infinite publish loop. The counter is per thread
        /// and shared by all bus instances; the limit is generous enough that legitimate chains never reach it.
        /// </summary>
        private const int MaxPublishDepth = 64;

        [ThreadStatic] private static int publishDepth;

        // Set once a runaway chain is reported; every remaining publish of that chain on this thread is then
        // skipped so the stack unwinds in linear time instead of exploring each fan-out branch to the limit.
        [ThreadStatic] private static bool isRunawayChainCancelled;
#endif

        // Listener groups indexed by EventTypeIndex<TEvent>.Value. Publish reads lock-free; Subscribe fills an
        // empty slot or swaps in a grown copy under mutationLock.
        private volatile IEventListeners[] groups = Array.Empty<IEventListeners>();

        // Serializes all mutations. Never held while listeners run, so handlers may publish, subscribe, or
        // unsubscribe re-entrantly without deadlocking.
        private readonly object mutationLock = new();

        private volatile bool isDisposed;

#if UNITY_EDITOR
        // Identifies this instance in the editor-only Event Debugger window.
        private readonly int debugId;
#endif

        /// <summary>
        /// Creates an empty bus. In the editor, the instance registers itself with the
        /// Event Debugger window; player builds skip registration entirely.
        /// </summary>
        public EventBus()
        {
#if UNITY_EDITOR
            debugId = EventBusDebugger.Register();
#endif
        }

        /// <summary>
        /// Names this bus in the editor's Event Debugger window in place of the default
        /// "EventBus N". Editor-only: calls to this method are stripped from player builds by the
        /// compiler, so it can be called unconditionally from runtime code at zero build cost.
        /// </summary>
        /// <param name="name">The display name shown in the debugger window. Avoid ending it with
        /// Unity menu shortcut notation ('#', '%', '&amp;' or '_' followed by a character), which
        /// editor dropdowns would render as a keyboard shortcut instead of text.</param>
        [Conditional("UNITY_EDITOR")]
        public void SetDebugName(string name)
        {
#if UNITY_EDITOR
            EventBusDebugger.SetBusName(debugId, name);
#endif
        }

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException">The bus has been disposed.</exception>
        public void Publish<TEvent>(TEvent eventData)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(EventBus));
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int depth = publishDepth;
            if (depth > 0 && isRunawayChainCancelled)
            {
                return;
            }
#endif

#if UNITY_EDITOR
            // Editor-only trace for the Event Debugger window. Recorded before the subscriber
            // checks so that events nobody listens to still show up as "fired, went nowhere".
            EventBusDebugger.OnPublish(debugId, typeof(TEvent), depth);
#endif

            // No subscribers for this event type (yet) is a normal situation, not an error: the listening
            // system may simply not be active. Deliberately a silent no-op.
            EventListeners<TEvent> group = FindGroup<TEvent>();
            if (group == null)
            {
                return;
            }

            Subscription<TEvent>[] subscriptions = group.GetSubscriptions(out int length);
            if (length == 0)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (depth >= MaxPublishDepth)
            {
                CancelRunawayChain<TEvent>();
                return;
            }

            publishDepth = depth + 1;
            try
            {
#endif
                for (int i = 0; i < length; i++)
                {
                    Subscription<TEvent> subscription = subscriptions[i];
                    if (subscription == null || !subscription.IsActive)
                    {
                        continue;
                    }

                    if (subscription.IsDestroyed)
                    {
                        RemoveDestroyedListener(group, subscription);
                        continue;
                    }

#if UNITY_EDITOR
                    // Record the delivery one level deeper than its publish, and mark this listener
                    // as the executing handler so any events it publishes are attributed to it.
                    string listenerName = subscription.Listener.GetType().Name;
                    EventBusDebugger.OnDelivery(debugId, typeof(TEvent), listenerName, publishDepth);
                    string previousHandler = EventBusDebugger.BeginHandler(listenerName);
                    try
                    {
#endif
                    try
                    {
                        subscription.Listener.Handle(eventData);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"Listener '{subscription.Listener.GetType().Name}' threw while handling '{typeof(TEvent).Name}':\n{exception}");
                    }
#if UNITY_EDITOR
                    }
                    finally
                    {
                        EventBusDebugger.EndHandler(previousHandler);
                    }
#endif
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            }
            finally
            {
                publishDepth = depth;
                if (depth == 0)
                {
                    isRunawayChainCancelled = false;
                }
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

            bool isDuplicate;
            lock (mutationLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(EventBus));
                }
                isDuplicate = !GetOrCreateGroup<TEvent>().Add(listener);
            }

            if (isDuplicate)
            {
                // A duplicate subscription would make the listener handle every event twice.
                Debug.LogWarning(
                    $"Listener '{listener.GetType().Name}' is already subscribed to '{typeof(TEvent).Name}'; duplicate subscription ignored.");
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

            // Unsubscribing after disposal is a no-op rather than an error: teardown code (OnDisable/OnDestroy)
            // routinely runs after the bus's owner has disposed it.
            if (isDisposed)
            {
                return;
            }

            lock (mutationLock)
            {
                if (!isDisposed)
                {
                    FindGroup<TEvent>()?.Remove(listener);
                }
            }
        }

        /// <summary>
        /// Releases all listeners and stops any delivery still in progress. After disposal,
        /// <see cref="Publish{TEvent}"/> and <see cref="Subscribe{TEvent}"/> throw <see cref="ObjectDisposedException"/>,
        /// while <see cref="Unsubscribe{TEvent}"/> becomes a no-op so teardown code stays safe.
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

                IEventListeners[] current = groups;
                groups = Array.Empty<IEventListeners>();
                foreach (IEventListeners group in current)
                {
                    group?.Clear();
                }
            }

#if UNITY_EDITOR
            EventBusDebugger.Unregister(debugId);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EventListeners<TEvent> FindGroup<TEvent>()
        {
            int typeIndex = EventTypeIndex<TEvent>.Value;
            IEventListeners[] current = groups;
            return (uint)typeIndex < (uint)current.Length ? (EventListeners<TEvent>)current[typeIndex] : null;
        }

        /// <summary>Must be called under <see cref="mutationLock"/>.</summary>
        private EventListeners<TEvent> GetOrCreateGroup<TEvent>()
        {
            EventListeners<TEvent> group = FindGroup<TEvent>();
            if (group != null)
            {
                return group;
            }

            group = new EventListeners<TEvent>();
            int typeIndex = EventTypeIndex<TEvent>.Value;
            IEventListeners[] current = groups;
            if (typeIndex < current.Length)
            {
                // Release semantics: a lock-free reader that sees the group also sees it fully constructed.
                Volatile.Write(ref current[typeIndex], group);
            }
            else
            {
                IEventListeners[] grown = new IEventListeners[Math.Max(typeIndex + 1, current.Length + current.Length / 2)];
                Array.Copy(current, grown, current.Length);
                grown[typeIndex] = group;
                groups = grown;
            }
            return group;
        }

        private void RemoveDestroyedListener<TEvent>(EventListeners<TEvent> group, Subscription<TEvent> subscription)
        {
            bool isRemoved;
            lock (mutationLock)
            {
                isRemoved = !isDisposed && group.Remove(subscription);
            }

            if (isRemoved)
            {
                WarnDestroyedListener<TEvent>(subscription);
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void WarnDestroyedListener<TEvent>(Subscription<TEvent> subscription)
        {
            Debug.LogWarning(
                $"Listener '{subscription.Listener.GetType().Name}' was destroyed while still subscribed to '{typeof(TEvent).Name}' " +
                "and has been unsubscribed automatically. Unsubscribe in OnDisable or OnDestroy to release it deterministically.");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void CancelRunawayChain<TEvent>()
        {
            isRunawayChainCancelled = true;
            Debug.LogError(
                $"Publish depth exceeded {MaxPublishDepth} while publishing '{typeof(TEvent).Name}': listeners are most likely " +
                "publishing events that recursively trigger each other. The rest of this event chain was cancelled. " +
                "This check exists only in the editor and development builds; a release build would overflow the stack.");
        }
#endif
    }
}
