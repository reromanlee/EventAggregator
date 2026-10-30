using System;
using System.Collections.Generic;
using System.Threading;

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
    /// The listeners subscribed to one event type, kept in an append-only array that publishers read through an
    /// immutable <see cref="ListenerSnapshot{TEvent}"/> without locking or allocating.
    /// </summary>
    /// <remarks>
    /// <para>Subscribing appends into spare capacity and publishes a new snapshot. Unsubscribing deactivates the
    /// subscription and clears its slot, which releases the listener immediately and leaves a hole. Holes are
    /// compacted into a fresh array once they outnumber live subscriptions, so every mutation is amortized O(1)
    /// and bursts of thousands of subscriptions produce linear, not quadratic, garbage.</para>
    /// <para>Within one array a slot is written once and afterwards only cleared, and compaction always starts a
    /// new array, so a publish iterating any snapshot visits each live listener at most once and never one added
    /// after it started. Mutating members must be called under the owning bus's lock. <see cref="Snapshot"/> and
    /// <see cref="ActiveCount"/> may be read from any thread after the bus's acquire read of its group array.</para>
    /// </remarks>
    /// <typeparam name="TEvent">The event type this group's listeners handle.</typeparam>
    internal sealed class EventListeners<TEvent> : IEventListeners
    {
        private const int MinimumCapacity = 4;

        // Up to this many listeners a linear scan beats hashing; above it, duplicate checks and removals
        // switch to a lookup so large groups stay O(1) per mutation.
        private const int LookupThreshold = 8;

        // Written with release semantics; read plainly by publishers, whose preceding acquire orders the read.
        private ListenerSnapshot<TEvent> snapshot = ListenerSnapshot<TEvent>.Empty;
        private int activeCount;
        private int holeCount;
        private Dictionary<IEventListener<TEvent>, Subscription<TEvent>> lookup;

        /// <summary>The subscriptions to dispatch to. Never null.</summary>
        public ListenerSnapshot<TEvent> Snapshot => snapshot;

        /// <summary>The number of subscribed listeners.</summary>
        public int ActiveCount => activeCount;

        /// <summary>Subscribes a listener unless it is already subscribed. Returns false on a duplicate.</summary>
        public bool Add(IEventListener<TEvent> listener)
        {
            if (Find(listener) != null)
            {
                return false;
            }

            Subscription<TEvent>[] subscriptions = snapshot.Subscriptions;
            int count = snapshot.Count;
            if (count == subscriptions.Length)
            {
                subscriptions = Compact(Math.Max(MinimumCapacity, (activeCount + 1) * 2), out count);
            }

            Subscription<TEvent> subscription = new(listener, count);
            subscriptions[count] = subscription;
            Publish(subscriptions, count + 1);
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

            Subscription<TEvent>[] subscriptions = snapshot.Subscriptions;
            int index = subscription.Index;
            if ((uint)index < (uint)subscriptions.Length && ReferenceEquals(subscriptions[index], subscription))
            {
                subscriptions[index] = null;
                holeCount++;
            }

            if (holeCount > activeCount)
            {
                Subscription<TEvent>[] compacted = Compact(Math.Max(MinimumCapacity, activeCount * 2), out int count);
                Publish(compacted, count);
            }
            return true;
        }

        public void Clear()
        {
            Subscription<TEvent>[] subscriptions = snapshot.Subscriptions;
            int count = snapshot.Count;
            for (int i = 0; i < count; i++)
            {
                subscriptions[i]?.Deactivate();
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

            Subscription<TEvent>[] subscriptions = snapshot.Subscriptions;
            int count = snapshot.Count;
            for (int i = 0; i < count; i++)
            {
                Subscription<TEvent> subscription = subscriptions[i];
                if (subscription != null && ReferenceEquals(subscription.Listener, listener))
                {
                    return subscription;
                }
            }
            return null;
        }

        /// <summary>Copies live subscriptions, in order, into a new array. The caller publishes it.</summary>
        private Subscription<TEvent>[] Compact(int capacity, out int count)
        {
            Subscription<TEvent>[] current = snapshot.Subscriptions;
            int used = snapshot.Count;
            Subscription<TEvent>[] next = new Subscription<TEvent>[capacity];
            count = 0;
            for (int i = 0; i < used; i++)
            {
                Subscription<TEvent> subscription = current[i];
                if (subscription != null)
                {
                    subscription.Index = count;
                    next[count++] = subscription;
                }
            }
            holeCount = 0;
            return next;
        }

        /// <summary>Release semantics: a publisher that sees the snapshot also sees every slot it covers.</summary>
        private void Publish(Subscription<TEvent>[] subscriptions, int count)
        {
            Volatile.Write(ref snapshot, new ListenerSnapshot<TEvent>(subscriptions, count));
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<IEventListener<TEvent>, Subscription<TEvent>>(
                activeCount * 2, IdentityComparer<IEventListener<TEvent>>.Instance);

            Subscription<TEvent>[] subscriptions = snapshot.Subscriptions;
            int count = snapshot.Count;
            for (int i = 0; i < count; i++)
            {
                Subscription<TEvent> subscription = subscriptions[i];
                if (subscription != null)
                {
                    lookup.Add(subscription.Listener, subscription);
                }
            }
        }

        /// <summary>Drops the array and lookup once the group is empty so an idle event type holds no memory.</summary>
        private void Reset()
        {
            Volatile.Write(ref snapshot, ListenerSnapshot<TEvent>.Empty);
            holeCount = 0;
            lookup = null;
        }
    }
}
