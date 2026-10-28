using System;
using System.Collections.Generic;

namespace reromanlee.EventAggregator
{
    /// <summary>
    /// Non-generic view of <see cref="EventListeners{TEvent}"/> so a bus can keep groups of different event
    /// types in one array and release them uniformly on dispose.
    /// </summary>
    internal interface IEventListeners
    {
        /// <summary>Deactivates every subscription and releases all listeners.</summary>
        void Clear();
    }

    /// <summary>
    /// The listeners subscribed to one event type, kept in an append-only array that publishers read without
    /// locking or allocating.
    /// </summary>
    /// <remarks>
    /// <para>Subscribing appends into spare capacity. Unsubscribing deactivates the subscription and clears its
    /// slot, which releases the listener immediately and leaves a hole. Holes are compacted into a fresh array
    /// once they outnumber live subscriptions, so every mutation is amortized O(1) and bursts of thousands of
    /// subscriptions produce linear, not quadratic, garbage.</para>
    /// <para>Within one array instance a slot is written once and afterwards only cleared, and compaction always
    /// publishes a new array, so a publish iterating any array it obtained visits each live listener at most
    /// once and never one added after it started. Mutating members must be called under the owning bus's lock;
    /// <see cref="GetSubscriptions"/> and <see cref="ActiveCount"/> may be read from any thread.</para>
    /// </remarks>
    /// <typeparam name="TEvent">The event type this group's listeners handle.</typeparam>
    internal sealed class EventListeners<TEvent> : IEventListeners
    {
        private const int MinimumCapacity = 4;

        // Up to this many listeners a linear scan beats hashing; above it, duplicate checks and removals
        // switch to a lookup so large groups stay O(1) per mutation.
        private const int LookupThreshold = 8;

        // Writers publish the array before the used count and readers load the count before the array, so a
        // reader never indexes past a slot it is guaranteed to see.
        private volatile Subscription<TEvent>[] subscriptions = Array.Empty<Subscription<TEvent>>();
        private volatile int usedCount;
        private volatile int activeCount;
        private int holeCount;
        private Dictionary<IEventListener<TEvent>, Subscription<TEvent>> lookup;

        /// <summary>The number of subscribed listeners. Lock-free.</summary>
        public int ActiveCount => activeCount;

        /// <summary>
        /// Returns the array to dispatch from and how many of its slots to visit. Null slots are holes left by
        /// removals; inactive entries belong to listeners removed while this array was in use. Lock-free.
        /// </summary>
        public Subscription<TEvent>[] GetSubscriptions(out int length)
        {
            int used = usedCount;
            Subscription<TEvent>[] current = subscriptions;
            length = used < current.Length ? used : current.Length;
            return current;
        }

        /// <summary>Subscribes a listener unless it is already subscribed. Returns false on a duplicate.</summary>
        public bool Add(IEventListener<TEvent> listener)
        {
            if (Find(listener) != null)
            {
                return false;
            }

            Subscription<TEvent>[] current = subscriptions;
            int used = usedCount;
            if (used == current.Length)
            {
                current = Rebuild(Math.Max(MinimumCapacity, (activeCount + 1) * 2));
                used = usedCount;
            }

            Subscription<TEvent> subscription = new(listener, used);
            current[used] = subscription;
            usedCount = used + 1;
            activeCount++;

            if (lookup != null)
            {
                lookup.Add(listener, subscription);
            }
            else if (activeCount > LookupThreshold)
            {
                BuildLookup();
            }
            return true;
        }

        /// <summary>Unsubscribes a listener. Returns false when it was not subscribed.</summary>
        public bool Remove(IEventListener<TEvent> listener)
        {
            Subscription<TEvent> subscription = Find(listener);
            return subscription != null && Remove(subscription);
        }

        /// <summary>Removes a subscription obtained during a publish. Returns false when it was already removed.</summary>
        public bool Remove(Subscription<TEvent> subscription)
        {
            if (!subscription.IsActive)
            {
                return false;
            }

            subscription.Deactivate();
            lookup?.Remove(subscription.Listener);
            activeCount--;

            if (activeCount == 0)
            {
                Reset();
                return true;
            }

            Subscription<TEvent>[] current = subscriptions;
            int index = subscription.Index;
            if ((uint)index < (uint)current.Length && ReferenceEquals(current[index], subscription))
            {
                current[index] = null;
                holeCount++;
            }

            if (holeCount > activeCount)
            {
                Rebuild(Math.Max(MinimumCapacity, activeCount * 2));
            }
            return true;
        }

        public void Clear()
        {
            Subscription<TEvent>[] current = subscriptions;
            int used = usedCount;
            for (int i = 0; i < used; i++)
            {
                current[i]?.Deactivate();
            }
            activeCount = 0;
            Reset();
        }

        private Subscription<TEvent> Find(IEventListener<TEvent> listener)
        {
            if (lookup != null)
            {
                return lookup.TryGetValue(listener, out Subscription<TEvent> found) ? found : null;
            }

            Subscription<TEvent>[] current = subscriptions;
            int used = usedCount;
            for (int i = 0; i < used; i++)
            {
                Subscription<TEvent> subscription = current[i];
                if (subscription != null && ReferenceEquals(subscription.Listener, listener))
                {
                    return subscription;
                }
            }
            return null;
        }

        /// <summary>Copies live subscriptions, in order, into a new array and publishes it.</summary>
        private Subscription<TEvent>[] Rebuild(int capacity)
        {
            Subscription<TEvent>[] current = subscriptions;
            int used = usedCount;
            Subscription<TEvent>[] next = new Subscription<TEvent>[capacity];
            int count = 0;
            for (int i = 0; i < used; i++)
            {
                Subscription<TEvent> subscription = current[i];
                if (subscription != null)
                {
                    subscription.Index = count;
                    next[count++] = subscription;
                }
            }

            subscriptions = next;
            usedCount = count;
            holeCount = 0;
            return next;
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<IEventListener<TEvent>, Subscription<TEvent>>(
                activeCount * 2, IdentityComparer<IEventListener<TEvent>>.Instance);

            Subscription<TEvent>[] current = subscriptions;
            int used = usedCount;
            for (int i = 0; i < used; i++)
            {
                Subscription<TEvent> subscription = current[i];
                if (subscription != null)
                {
                    lookup.Add(subscription.Listener, subscription);
                }
            }
        }

        /// <summary>Drops the array and lookup once the group is empty so an idle event type holds no memory.</summary>
        private void Reset()
        {
            subscriptions = Array.Empty<Subscription<TEvent>>();
            usedCount = 0;
            holeCount = 0;
            lookup = null;
        }
    }
}
