namespace reromanlee.EventAggregator
{
    /// <summary>
    /// A type-keyed publish/subscribe event bus that decouples event producers from consumers.
    /// Listeners subscribe per event type; publishing an event synchronously invokes every listener
    /// subscribed to that exact type.
    /// </summary>
    /// <remarks>
    /// Event types are matched exactly: publishing a derived event does not notify listeners of its
    /// base type. Prefer small structs or sealed classes as event payloads; struct events flow through
    /// the bus without boxing.
    /// </remarks>
    public interface IEventBus
    {
        /// <summary>
        /// Synchronously delivers <paramref name="eventData"/> to every listener currently subscribed
        /// to <typeparamref name="TEvent"/>, in subscription order.
        /// </summary>
        /// <remarks>
        /// Publishing an event that has no subscribers is a valid no-op. An exception thrown by one
        /// listener is caught and logged, and delivery continues with the remaining listeners.
        /// </remarks>
        /// <typeparam name="TEvent">The event type; listeners are matched by this exact type.</typeparam>
        /// <param name="eventData">The event payload passed to each listener.</param>
        void Publish<TEvent>(TEvent eventData);

        /// <summary>
        /// Subscribes <paramref name="listener"/> to events of type <typeparamref name="TEvent"/>.
        /// A listener instance already subscribed to the same event type is not added twice.
        /// </summary>
        /// <remarks>
        /// The bus holds a strong reference to the listener until it is unsubscribed, keeping the
        /// listener alive. Always pair with <see cref="Unsubscribe{TEvent}"/> when the listener's
        /// lifetime ends (e.g. in <c>OnDestroy</c>).
        /// </remarks>
        /// <typeparam name="TEvent">The event type to receive.</typeparam>
        /// <param name="listener">The listener whose <see cref="IEventListener{TEvent}.Handle"/> is invoked on publish.</param>
        void Subscribe<TEvent>(IEventListener<TEvent> listener);

        /// <summary>
        /// Removes the subscription of <paramref name="listener"/> to events of type
        /// <typeparamref name="TEvent"/>. Does nothing if the listener is not subscribed.
        /// </summary>
        /// <typeparam name="TEvent">The event type to stop receiving.</typeparam>
        /// <param name="listener">The previously subscribed listener.</param>
        void Unsubscribe<TEvent>(IEventListener<TEvent> listener);
    }
}