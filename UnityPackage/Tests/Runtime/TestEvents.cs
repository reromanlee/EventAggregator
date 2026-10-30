using System;
using System.Collections.Generic;
using System.Threading;

namespace reromanlee.EventAggregator.Tests
{
    internal readonly struct Ping
    {
        public readonly int Value;

        public Ping(int value)
        {
            Value = value;
        }
    }

    internal readonly struct Pong
    {
    }

    internal class BaseMessage
    {
    }

    internal sealed class DerivedMessage : BaseMessage
    {
    }

    /// <summary>Records every event it receives and optionally runs a callback while handling it.</summary>
    internal sealed class RecordingListener<TEvent> : IEventListener<TEvent>
    {
        public readonly List<TEvent> Received = new();
        public Action<TEvent> OnHandle;

        public int Count => Received.Count;

        public void Handle(TEvent eventData)
        {
            Received.Add(eventData);
            OnHandle?.Invoke(eventData);
        }
    }

    /// <summary>Counts events thread-safely without allocating, for concurrency and allocation tests.</summary>
    internal sealed class CountingListener<TEvent> : IEventListener<TEvent>
    {
        private int count;

        public int Count => Volatile.Read(ref count);

        public void Handle(TEvent eventData)
        {
            Interlocked.Increment(ref count);
        }
    }

    /// <summary>A listener of two event types, as a class would implement for several events.</summary>
    internal sealed class MultiListener : IEventListener<Ping>, IEventListener<Pong>
    {
        public int PingCount;
        public int PongCount;

        public void Handle(Ping eventData)
        {
            PingCount++;
        }

        public void Handle(Pong eventData)
        {
            PongCount++;
        }
    }

    /// <summary>Claims to equal every other instance, to prove the bus identifies listeners by reference.</summary>
    internal sealed class EqualsEverythingListener : IEventListener<Ping>
    {
        public int Count;

        public void Handle(Ping eventData)
        {
            Count++;
        }

        public override bool Equals(object obj)
        {
            return obj is EqualsEverythingListener;
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }

    internal struct StructListener : IEventListener<Ping>
    {
        public void Handle(Ping eventData)
        {
        }
    }

    /// <summary>Publishes the event it handles again, forming an infinite publish loop.</summary>
    internal sealed class RepublishingListener : IEventListener<Ping>
    {
        private readonly IEventBus bus;
        public int Count;

        public RepublishingListener(IEventBus bus)
        {
            this.bus = bus;
        }

        public void Handle(Ping eventData)
        {
            Count++;
            bus.Publish(new Ping(eventData.Value + 1));
        }
    }
}
