namespace reromanlee.EventAggregator
{
    /// <summary>
    /// One listener's subscription to one event type. Every listener array that contains it shares this
    /// instance, so deactivating it stops delivery even from a publish that is already in progress.
    /// </summary>
    /// <typeparam name="TEvent">The event type the listener handles.</typeparam>
    internal sealed class Subscription<TEvent>
    {
        public readonly IEventListener<TEvent> Listener;

        /// <summary>Slot in the owning group's current array. Only read or written under the bus lock.</summary>
        public int Index;

        private volatile bool isActive = true;

        public Subscription(IEventListener<TEvent> listener, int index)
        {
            Listener = listener;
            Index = index;
        }

        public bool IsActive => isActive;

        public void Deactivate()
        {
            isActive = false;
        }
    }
}
