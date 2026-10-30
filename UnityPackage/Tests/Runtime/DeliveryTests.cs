using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace reromanlee.EventAggregator.Tests
{
    public sealed class DeliveryTests
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
        public void Publish_DeliversPayloadToSubscribedListener()
        {
            RecordingListener<Ping> listener = new();
            bus.Subscribe(listener);

            bus.Publish(new Ping(42));

            Assert.That(listener.Count, Is.EqualTo(1));
            Assert.That(listener.Received[0].Value, Is.EqualTo(42));
        }

        [Test]
        public void Publish_WithoutSubscribers_IsSilentNoOp()
        {
            Assert.DoesNotThrow(() => bus.Publish(new Ping(1)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Publish_DeliversInSubscriptionOrder()
        {
            List<int> order = new();
            for (int i = 0; i < 5; i++)
            {
                int id = i;
                bus.Subscribe(new RecordingListener<Ping> { OnHandle = _ => order.Add(id) });
            }

            bus.Publish(new Ping());

            Assert.That(order, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
        }

        [Test]
        public void Publish_MatchesExactEventTypeOnly()
        {
            RecordingListener<BaseMessage> baseListener = new();
            bus.Subscribe(baseListener);

            bus.Publish(new DerivedMessage());
            Assert.That(baseListener.Count, Is.Zero, "A derived event must not reach listeners of its base type.");

            bus.Publish<BaseMessage>(new DerivedMessage());
            Assert.That(baseListener.Count, Is.EqualTo(1), "The type argument, not the runtime type, selects listeners.");
        }

        [Test]
        public void Subscribe_ContravariantListener_ReceivesDerivedEventsWhenSubscribedExplicitly()
        {
            RecordingListener<BaseMessage> baseListener = new();
            bus.Subscribe<DerivedMessage>(baseListener);

            bus.Publish(new DerivedMessage());

            Assert.That(baseListener.Count, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_OneListenerForSeveralEventTypes_ReceivesEach()
        {
            MultiListener listener = new();
            bus.Subscribe<Ping>(listener);
            bus.Subscribe<Pong>(listener);

            bus.Publish(new Ping());
            bus.Publish(new Pong());
            bus.Publish(new Pong());

            Assert.That(listener.PingCount, Is.EqualTo(1));
            Assert.That(listener.PongCount, Is.EqualTo(2));
        }

        [Test]
        public void Publish_ThrowingListener_IsLoggedAndOthersStillReceive()
        {
            RecordingListener<Ping> thrower = new() { OnHandle = _ => throw new InvalidOperationException("listener failure") };
            RecordingListener<Ping> after = new();
            bus.Subscribe(thrower);
            bus.Subscribe(after);

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: listener failure"));
            bus.Publish(new Ping());

            Assert.That(after.Count, Is.EqualTo(1));
        }

        [Test]
        public void Publish_FromHandler_DeliversNestedEvent()
        {
            RecordingListener<Pong> pongListener = new();
            RecordingListener<Ping> pingListener = new() { OnHandle = _ => bus.Publish(new Pong()) };
            bus.Subscribe(pingListener);
            bus.Subscribe(pongListener);

            bus.Publish(new Ping());

            Assert.That(pongListener.Count, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_DuringPublish_StopsInFlightDelivery()
        {
            RecordingListener<Ping> second = new();
            RecordingListener<Ping> first = new() { OnHandle = _ => bus.Unsubscribe(second) };
            bus.Subscribe(first);
            bus.Subscribe(second);

            bus.Publish(new Ping());

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(second.Count, Is.Zero, "A listener unsubscribed mid-publish must not receive the in-flight event.");
        }

        [Test]
        public void Unsubscribe_SelfDuringPublish_OthersStillReceive()
        {
            RecordingListener<Ping> self = new();
            self.OnHandle = _ => bus.Unsubscribe(self);
            RecordingListener<Ping> other = new();
            bus.Subscribe(self);
            bus.Subscribe(other);

            bus.Publish(new Ping());
            bus.Publish(new Ping());

            Assert.That(self.Count, Is.EqualTo(1));
            Assert.That(other.Count, Is.EqualTo(2));
        }

        [Test]
        public void Subscribe_DuringPublish_TakesEffectFromNextPublish()
        {
            RecordingListener<Ping> late = new();
            RecordingListener<Ping> first = new();
            first.OnHandle = _ =>
            {
                bus.Subscribe(late);
                first.OnHandle = null;
            };
            bus.Subscribe(first);

            bus.Publish(new Ping());
            Assert.That(late.Count, Is.Zero);

            bus.Publish(new Ping());
            Assert.That(late.Count, Is.EqualTo(1));
        }

        [Test]
        public void MassChurnDuringPublish_RemovedSkippedAndNewcomersWaitForNextPublish()
        {
            List<RecordingListener<Ping>> originals = new();
            List<RecordingListener<Ping>> newcomers = new();
            for (int i = 0; i < 5; i++)
            {
                originals.Add(new RecordingListener<Ping>());
            }
            for (int i = 0; i < 20; i++)
            {
                newcomers.Add(new RecordingListener<Ping>());
            }

            originals[0].OnHandle = _ =>
            {
                originals[0].OnHandle = null;
                foreach (RecordingListener<Ping> original in originals)
                {
                    bus.Unsubscribe(original);
                }
                foreach (RecordingListener<Ping> newcomer in newcomers)
                {
                    bus.Subscribe(newcomer);
                }
            };
            foreach (RecordingListener<Ping> original in originals)
            {
                bus.Subscribe(original);
            }

            bus.Publish(new Ping());
            Assert.That(originals.ConvertAll(listener => listener.Count), Is.EqualTo(new[] { 1, 0, 0, 0, 0 }));
            Assert.That(newcomers.TrueForAll(listener => listener.Count == 0), "Newcomers must wait for the next publish.");

            bus.Publish(new Ping());
            Assert.That(newcomers.TrueForAll(listener => listener.Count == 1));
            Assert.That(originals[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_DuringPublish_StopsRemainingDeliveries()
        {
            RecordingListener<Ping> disposer = new() { OnHandle = _ => bus.Dispose() };
            RecordingListener<Ping> after = new();
            bus.Subscribe(disposer);
            bus.Subscribe(after);

            bus.Publish(new Ping());

            Assert.That(after.Count, Is.Zero);
        }
    }
}
