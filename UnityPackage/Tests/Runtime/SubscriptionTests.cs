using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace reromanlee.EventAggregator.Tests
{
    public sealed class SubscriptionTests
    {
        private EventBus bus;

        [SetUp]
        public void SetUp()
        {
            bus = new EventBus();
        }

        [TearDown]
        public void TearDown()
        {
            bus.Dispose();
        }

        [Test]
        public void Subscribe_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<Ping>(null));
        }

        [Test]
        public void Unsubscribe_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => bus.Unsubscribe<Ping>(null));
        }

        [Test]
        public void Subscribe_StructListener_Throws()
        {
            Assert.Throws<ArgumentException>(() => bus.Subscribe<Ping>(new StructListener()));
        }

        [Test]
        public void Subscribe_Duplicate_IsIgnoredWithWarning()
        {
            RecordingListener<Ping> listener = new();
            bus.Subscribe(listener);

            LogAssert.Expect(LogType.Warning, new Regex("already subscribed"));
            bus.Subscribe(listener);
            bus.Publish(new Ping());

            Assert.That(listener.Count, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            RecordingListener<Ping> listener = new();
            bus.Subscribe(listener);
            bus.Unsubscribe(listener);

            bus.Publish(new Ping());

            Assert.That(listener.Count, Is.Zero);
        }

        [Test]
        public void Unsubscribe_NotSubscribed_IsNoOp()
        {
            Assert.DoesNotThrow(() => bus.Unsubscribe(new RecordingListener<Ping>()));
        }

        [Test]
        public void Resubscribe_AfterUnsubscribe_DeliversAgain()
        {
            RecordingListener<Ping> listener = new();
            bus.Subscribe(listener);
            bus.Unsubscribe(listener);
            bus.Subscribe(listener);

            bus.Publish(new Ping());

            Assert.That(listener.Count, Is.EqualTo(1));
        }

        [Test]
        public void ListenerIdentity_IgnoresEqualsOverrides()
        {
            EqualsEverythingListener first = new();
            EqualsEverythingListener second = new();
            bus.Subscribe(first);
            bus.Subscribe(second);
            bus.Unsubscribe(first);

            bus.Publish(new Ping());

            Assert.That(first.Count, Is.Zero);
            Assert.That(second.Count, Is.EqualTo(1));
        }

        [Test]
        public void HasListeners_TracksSubscriptions()
        {
            RecordingListener<Ping> listener = new();
            Assert.That(bus.HasListeners<Ping>(), Is.False);

            bus.Subscribe(listener);
            Assert.That(bus.HasListeners<Ping>(), Is.True);
            Assert.That(bus.HasListeners<Pong>(), Is.False);

            bus.Unsubscribe(listener);
            Assert.That(bus.HasListeners<Ping>(), Is.False);
        }

        [Test]
        public void LargeGroup_KeepsOrderAcrossRemovalsAndReadds([Values(3, 9, 100, 1000)] int count)
        {
            List<int> order = new();
            List<RecordingListener<Ping>> listeners = new();
            for (int i = 0; i < count; i++)
            {
                int id = i;
                RecordingListener<Ping> listener = new() { OnHandle = _ => order.Add(id) };
                listeners.Add(listener);
                bus.Subscribe(listener);
            }

            List<int> expected = new();
            for (int i = 0; i < count; i += 2)
            {
                bus.Unsubscribe(listeners[i]);
            }
            for (int i = 1; i < count; i += 2)
            {
                expected.Add(i);
            }
            for (int i = 0; i < count; i += 4)
            {
                bus.Subscribe(listeners[i]);
                expected.Add(i);
            }

            bus.Publish(new Ping());

            Assert.That(order, Is.EqualTo(expected));
        }

        [Test]
        public void SubscribeUnsubscribeChurn_WithoutPublishing_StaysCorrect()
        {
            RecordingListener<Ping> stable = new();
            bus.Subscribe(stable);
            RecordingListener<Ping>[] pool = new RecordingListener<Ping>[32];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = new RecordingListener<Ping>();
            }

            for (int round = 0; round < 10000; round++)
            {
                bus.Subscribe(pool[round % pool.Length]);
                bus.Unsubscribe(pool[(round + pool.Length / 2) % pool.Length]);
            }
            foreach (RecordingListener<Ping> pooled in pool)
            {
                bus.Unsubscribe(pooled);
            }

            bus.Publish(new Ping());

            Assert.That(stable.Count, Is.EqualTo(1));
            Assert.That(Array.TrueForAll(pool, pooled => pooled.Count == 0));
        }
    }
}
