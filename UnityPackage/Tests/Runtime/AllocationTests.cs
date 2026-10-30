using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace reromanlee.EventAggregator.Tests
{
    public sealed class AllocationTests
    {
        private EventBus bus;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            // A recording Event Debugger window traces every publish, which allocates by design.
            Assume.That(EventBusDebugger.IsRecording, Is.False, "Close the Event Debugger window to measure allocations.");
#endif
            bus = new EventBus();
        }

        [TearDown]
        public void TearDown()
        {
            bus?.Dispose();
        }

        [Test]
        public void Publish_StructEventToListeners_AllocatesNothing()
        {
            for (int i = 0; i < 10; i++)
            {
                bus.Subscribe(new CountingListener<Pong>());
            }
            bus.Publish(new Pong());

            Assert.That(() => bus.Publish(new Pong()), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Publish_WithoutListeners_AllocatesNothing()
        {
            bus.Publish(new Ping());

            Assert.That(() => bus.Publish(new Ping()), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void HasListeners_AllocatesNothing()
        {
            bus.Subscribe(new CountingListener<Ping>());
            bus.HasListeners<Ping>();

            Assert.That(new TestDelegate(() => bus.HasListeners<Ping>()), Is.Not.AllocatingGCMemory());
        }
    }
}
