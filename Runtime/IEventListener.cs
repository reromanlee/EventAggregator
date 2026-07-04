namespace reromanlee.EventAggregator
{
    /// <summary>
    /// Receives events of type <typeparamref name="TEvent"/> published through an <see cref="IEventBus"/>.
    /// Implement this interface and pass the instance to <see cref="IEventBus.Subscribe{TEvent}"/> to start
    /// receiving events, and to <see cref="IEventBus.Unsubscribe{TEvent}"/> to stop.
    /// </summary>
    /// <remarks>
    /// A single class may implement this interface multiple times to handle several event types.
    /// The type parameter is contravariant, so a listener of a base event type can be subscribed
    /// where a listener of a derived reference type is expected.
    /// </remarks>
    /// <typeparam name="TEvent">The type of event this listener handles.</typeparam>
    public interface IEventListener<in TEvent>
    {
        /// <summary>
        /// Invoked by the event bus for every published event of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <remarks>
        /// Runs synchronously on the publishing thread. Exceptions thrown here are caught and logged
        /// by the bus, so a faulty listener cannot prevent other listeners from receiving the event.
        /// </remarks>
        /// <param name="eventData">The published event payload.</param>
        void Handle(TEvent eventData);
    }
}