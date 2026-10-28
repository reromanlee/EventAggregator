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

        /// <summary>
        /// The listener as a Unity object, or null for plain C# listeners. Resolved once at subscribe time so
        /// the publish path can detect destroyed listeners without a type check per delivery.
        /// </summary>
        public readonly UnityEngine.Object UnityObject;

        /// <summary>Slot in the owning group's current array. Only read or written under the bus lock.</summary>
        public int Index;

        private volatile bool isActive = true;

        public Subscription(IEventListener<TEvent> listener, int index)
        {
            Listener = listener;
            UnityObject = listener as UnityEngine.Object;
            Index = index;
        }

        public bool IsActive => isActive;

        /// <summary>True when the listener is a Unity object whose native counterpart has been destroyed.</summary>
        public bool IsDestroyed => UnityObject is not null && UnityObject == null;

        public void Deactivate()
        {
            isActive = false;
        }
    }
}
