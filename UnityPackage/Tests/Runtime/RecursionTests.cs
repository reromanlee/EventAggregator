#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Diagnostics;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace reromanlee.EventAggregator.Tests
{
    /// <summary>The recursion guard exists only in the editor and development builds, and so do these tests.</summary>
    public sealed class RecursionTests
    {
        [Test]
        public void InfiniteLoop_IsReportedInsteadOfOverflowingTheStack()
        {
            using EventBus bus = new();
            RepublishingListener listener = new(bus);
            bus.Subscribe(listener);

            LogAssert.Expect(LogType.Error, new Regex("Publish depth exceeded"));
            bus.Publish(new Ping());

            Assert.That(listener.Count, Is.EqualTo(64));
        }

        [Test]
        public void FanOutLoop_IsCancelledInLinearTime()
        {
            using EventBus bus = new();
            RepublishingListener first = new(bus);
            RepublishingListener second = new(bus);
            bus.Subscribe(first);
            bus.Subscribe(second);

            LogAssert.Expect(LogType.Error, new Regex("Publish depth exceeded"));
            Stopwatch stopwatch = Stopwatch.StartNew();
            bus.Publish(new Ping());
            stopwatch.Stop();

            // Without cancelling the whole chain, two re-publishing listeners would run 2^64 handlers.
            Assert.That(first.Count + second.Count, Is.LessThanOrEqualTo(2 * 64));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000));
        }

        [Test]
        public void AfterRunawayChain_NextPublishIsDeliveredNormally()
        {
            using EventBus bus = new();
            RepublishingListener looping = new(bus);
            bus.Subscribe(looping);
            LogAssert.Expect(LogType.Error, new Regex("Publish depth exceeded"));
            bus.Publish(new Ping());
            bus.Unsubscribe(looping);

            RecordingListener<Ping> listener = new();
            bus.Subscribe(listener);
            bus.Publish(new Ping());

            Assert.That(listener.Count, Is.EqualTo(1));
        }

        [Test]
        public void DeepButFiniteChain_IsNotReported()
        {
            using EventBus bus = new();
            RecordingListener<Ping> listener = new();
            listener.OnHandle = eventData =>
            {
                if (eventData.Value < 32)
                {
                    bus.Publish(new Ping(eventData.Value + 1));
                }
            };
            bus.Subscribe(listener);

            bus.Publish(new Ping(1));

            Assert.That(listener.Count, Is.EqualTo(32));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
