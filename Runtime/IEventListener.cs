namespace reromanlee.EventAggregator
{
    public interface IEventListener<T>
    {
        void Handle(T eventData);
    }
}