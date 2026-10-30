using System;

namespace reromanlee.EventAggregator
{
    /// <summary>
    /// An immutable view of one event type's subscriptions: the array to dispatch from and how many of its slots
    /// are in use. Publishing it as one reference lets a reader see a consistent array and count without a second
    /// memory fence, which matters under IL2CPP, where every volatile read is a full fence.
    /// </summary>
    /// <typeparam name="TEvent">The event type the subscriptions handle.</typeparam>
    internal sealed class ListenerSnapshot<TEvent>
    {
        public static readonly ListenerSnapshot<TEvent> Empty = new(Array.Empty<Subscription<TEvent>>(), 0);

        /// <summary>Slots beyond <see cref="Count"/> belong to later snapshots; null slots are removed listeners.</summary>
        public readonly Subscription<TEvent>[] Subscriptions;

        public readonly int Count;

        public ListenerSnapshot(Subscription<TEvent>[] subscriptions, int count)
        {
            Subscriptions = subscriptions;
            Count = count;
        }
    }
}
