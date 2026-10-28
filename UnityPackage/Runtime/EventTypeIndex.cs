using System.Threading;

namespace reromanlee.EventAggregator
{
    /// <summary>
    /// Hands out small, process-wide unique indices for event types, so a bus finds the listeners of an
    /// event type with a plain array read instead of hashing <see cref="System.Type"/> on every publish.
    /// </summary>
    internal static class EventTypeIndex
    {
        private static int lastIndex = -1;

        internal static int Next()
        {
            return Interlocked.Increment(ref lastIndex);
        }
    }

    /// <summary>
    /// The index of <typeparamref name="TEvent"/>, assigned once by the thread-safe type initializer on first use.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    internal static class EventTypeIndex<TEvent>
    {
        internal static readonly int Value = EventTypeIndex.Next();
    }
}
