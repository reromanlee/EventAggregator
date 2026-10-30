using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace reromanlee.EventAggregator.Tests
{
    public sealed class LifetimeTests
    {
        [Test]
        public void PublishAfterDispose_IsIgnoredWithOneWarning()
        {
            EventBus bus = new();
            RecordingListener<Ping> listener = new();
            bus.Subscribe(listener);
            bus.Dispose();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogAssert.Expect(LogType.Warning, new Regex("published on a disposed EventBus"));
#endif
            bus.Publish(new Ping());
            bus.Publish(new Pong());

            Assert.That(listener.Count, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SubscribeAfterDispose_Throws()
        {
            EventBus bus = new();
            bus.Dispose();

            Assert.Throws<ObjectDisposedException>(() => bus.Subscribe(new RecordingListener<Ping>()));
        }

        [Test]
        public void TeardownCallsAfterDispose_AreSafe()
        {
            EventBus bus = new();
            RecordingListener<Ping> listener = new();
            bus.Subscribe(listener);
            bus.Dispose();

            Assert.DoesNotThrow(() =>
            {
                bus.Unsubscribe(listener);
                bus.Dispose();
            });
            Assert.That(bus.HasListeners<Ping>(), Is.False);
        }

        [Test]
        public void DestroyedMonoBehaviourListener_IsSkippedAndUnsubscribed()
        {
            using EventBus bus = new();
            GameObject gameObject = new(nameof(DestroyedMonoBehaviourListener_IsSkippedAndUnsubscribed));
            DestroyableBehaviourListener listener = gameObject.AddComponent<DestroyableBehaviourListener>();
            bus.Subscribe(listener);
            Object.DestroyImmediate(gameObject);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogAssert.Expect(LogType.Warning, new Regex("was destroyed while still subscribed"));
#endif
            bus.Publish(new Ping());
            bus.Publish(new Ping());

            Assert.That(listener.Count, Is.Zero);
            Assert.That(bus.HasListeners<Ping>(), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DestroyedScriptableObjectListener_IsSkippedAndUnsubscribed()
        {
            using EventBus bus = new();
            DestroyableAssetListener listener = ScriptableObject.CreateInstance<DestroyableAssetListener>();
            RecordingListener<Ping> survivor = new();
            bus.Subscribe(listener);
            bus.Subscribe(survivor);
            Object.DestroyImmediate(listener);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogAssert.Expect(LogType.Warning, new Regex("was destroyed while still subscribed"));
#endif
            bus.Publish(new Ping());

            Assert.That(listener.Count, Is.Zero);
            Assert.That(survivor.Count, Is.EqualTo(1));
        }

        [Test]
        public void LiveUnityObjectListener_ReceivesEvents()
        {
            using EventBus bus = new();
            DestroyableAssetListener listener = ScriptableObject.CreateInstance<DestroyableAssetListener>();
            try
            {
                bus.Subscribe(listener);
                bus.Publish(new Ping());

                Assert.That(listener.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(listener);
            }
        }
    }
}
