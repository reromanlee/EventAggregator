namespace reromanlee.EventAggregator
{
    public interface IEventBus
    {
        void Publish<E>(E eventData);
        void Subscribe<E>(IEventListener<E> listener);
        void Unsubscribe<E>(IEventListener<E> listener);
    }
}