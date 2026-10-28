using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace reromanlee.EventAggregator.Tests
{
    /// <summary>WebGL players have no threads, so these tests run everywhere else.</summary>
    [UnityPlatform(exclude = new[] { RuntimePlatform.WebGLPlayer })]
    public sealed class ConcurrencyTests
    {
        [Test]
        public void ConcurrentPublishWithChurn_DeliversEveryEventToStableListener()
        {
            const int PublisherCount = 4;
            const int PublishesPerThread = 50000;

            using EventBus bus = new();
            CountingListener<Ping> stable = new();
            bus.Subscribe(stable);

            int isStopping = 0;
            Exception failure = null;
            Thread churn = new(() =>
            {
                try
                {
                    CountingListener<Ping>[] pool = new CountingListener<Ping>[64];
                    for (int i = 0; i < pool.Length; i++)
                    {
                        pool[i] = new CountingListener<Ping>();
                    }
                    for (int round = 0; Volatile.Read(ref isStopping) == 0; round++)
                    {
                        bus.Subscribe(pool[round % pool.Length]);
                        bus.Unsubscribe(pool[(round + pool.Length / 2) % pool.Length]);
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });

            List<Thread> publishers = new();
            for (int i = 0; i < PublisherCount; i++)
            {
                publishers.Add(new Thread(() =>
                {
                    try
                    {
                        for (int publish = 0; publish < PublishesPerThread; publish++)
                        {
                            bus.Publish(new Ping(publish));
                        }
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                }));
            }

            churn.Start();
            publishers.ForEach(thread => thread.Start());
            publishers.ForEach(thread => thread.Join());
            Volatile.Write(ref isStopping, 1);
            churn.Join();

            Assert.That(failure, Is.Null, failure?.ToString());
            Assert.That(stable.Count, Is.EqualTo(PublisherCount * PublishesPerThread));
        }

        [Test]
        public void ConcurrentSubscribers_AreAllRegisteredExactlyOnce()
        {
            const int ThreadCount = 4;
            const int ListenersPerThread = 2500;

            using EventBus bus = new();
            CountingListener<Ping>[][] batches = new CountingListener<Ping>[ThreadCount][];
            List<Thread> threads = new();
            for (int t = 0; t < ThreadCount; t++)
            {
                CountingListener<Ping>[] batch = batches[t] = new CountingListener<Ping>[ListenersPerThread];
                for (int i = 0; i < batch.Length; i++)
                {
                    batch[i] = new CountingListener<Ping>();
                }
                threads.Add(new Thread(() =>
                {
                    foreach (CountingListener<Ping> listener in batch)
                    {
                        bus.Subscribe(listener);
                    }
                }));
            }

            threads.ForEach(thread => thread.Start());
            threads.ForEach(thread => thread.Join());
            bus.Publish(new Ping());

            foreach (CountingListener<Ping>[] batch in batches)
            {
                Assert.That(Array.TrueForAll(batch, listener => listener.Count == 1));
            }
        }
    }
}
