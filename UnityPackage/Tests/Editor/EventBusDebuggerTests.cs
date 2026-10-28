using System.Collections.Generic;
using NUnit.Framework;

namespace reromanlee.EventAggregator.Editor.Tests
{
    public sealed class EventBusDebuggerTests
    {
        private readonly struct Request
        {
        }

        private readonly struct Response
        {
        }

        /// <summary>Answers every request with a response, forming a two-level chain.</summary>
        private sealed class Responder : IEventListener<Request>
        {
            private readonly IEventBus bus;

            public Responder(IEventBus bus)
            {
                this.bus = bus;
            }

            public void Handle(Request eventData)
            {
                bus.Publish(new Response());
            }
        }

        private sealed class ResponseSink : IEventListener<Response>
        {
            public void Handle(Response eventData)
            {
            }
        }

        [Test]
        public void WithoutRecorder_NothingIsRecorded()
        {
            Assume.That(EventBusDebugger.IsRecording, Is.False, "An open Event Debugger window is recording.");
            long since = LatestSequence();

            using EventBus bus = new();
            bus.Subscribe(new ResponseSink());
            bus.Publish(new Response());

            Assert.That(CopySince(since), Is.Empty);
        }

        [Test]
        public void Recorder_CapturesChainWithDepthsAndPublishers()
        {
            EventBusDebugger.AddRecorder();
            try
            {
                using EventBus bus = new();
                Responder responder = new(bus);
                bus.Subscribe(responder);
                bus.Subscribe(new ResponseSink());
                int busId = FindBusId(bus, nameof(Recorder_CapturesChainWithDepthsAndPublishers));
                long since = LatestSequence();

                bus.Publish(new Request());

                List<EventBusDebugger.TraceEntry> entries = CopySince(since).FindAll(entry => entry.BusId == busId);
                Assert.That(entries, Has.Count.EqualTo(4));
                AssertEntry(entries[0], EventBusDebugger.EntryKind.Publish, typeof(Request), typeof(EventBusDebuggerTests), 0);
                AssertEntry(entries[1], EventBusDebugger.EntryKind.Delivery, typeof(Request), typeof(Responder), 1);
                AssertEntry(entries[2], EventBusDebugger.EntryKind.Publish, typeof(Response), typeof(Responder), 1);
                AssertEntry(entries[3], EventBusDebugger.EntryKind.Delivery, typeof(Response), typeof(ResponseSink), 2);
                Assert.That(entries.TrueForAll(entry => entry.ChainId == entries[0].ChainId), "One root publish forms one chain.");
            }
            finally
            {
                EventBusDebugger.RemoveRecorder();
            }
        }

        [Test]
        public void DisposedBus_StaysListedUntilItsEntriesAreCleared()
        {
            EventBusDebugger.AddRecorder();
            try
            {
                EventBus bus = new();
                int busId = FindBusId(bus, nameof(DisposedBus_StaysListedUntilItsEntriesAreCleared));
                bus.Publish(new Response());
                bus.Dispose();

                Assert.That(GetBus(busId)?.IsDisposed, Is.True);

                EventBusDebugger.Clear();
                Assert.That(GetBus(busId), Is.Null);
            }
            finally
            {
                EventBusDebugger.RemoveRecorder();
            }
        }

        [Test]
        public void DisposedBusWithoutEntries_IsForgottenImmediately()
        {
            EventBus bus = new();
            int busId = FindBusId(bus, nameof(DisposedBusWithoutEntries_IsForgottenImmediately));
            bus.Dispose();

            Assert.That(GetBus(busId), Is.Null);
        }

        [Test]
        public void SetDebugName_WithWhitespace_RestoresDefaultName()
        {
            using EventBus bus = new();
            int busId = FindBusId(bus, "Temporary");

            bus.SetDebugName("   ");

            Assert.That(GetBus(busId)?.DisplayName, Is.EqualTo($"EventBus {busId}"));
        }

        [Test]
        public void Trace_IsBoundedByCapacityAndStaysContiguous()
        {
            EventBusDebugger.AddRecorder();
            try
            {
                using EventBus bus = new();
                for (int i = 0; i < EventBusDebugger.Capacity + 100; i++)
                {
                    bus.Publish(new Response());
                }

                List<EventBusDebugger.TraceEntry> entries = CopySince(-1);
                Assert.That(entries, Has.Count.EqualTo(EventBusDebugger.Capacity));
                for (int i = 1; i < entries.Count; i++)
                {
                    Assert.That(entries[i].Sequence, Is.EqualTo(entries[i - 1].Sequence + 1));
                }
            }
            finally
            {
                EventBusDebugger.RemoveRecorder();
            }
        }

        [Test]
        public void RemoveRecorder_NeverDropsBelowZero()
        {
            Assume.That(EventBusDebugger.IsRecording, Is.False, "An open Event Debugger window is recording.");

            EventBusDebugger.RemoveRecorder();
            EventBusDebugger.AddRecorder();
            Assert.That(EventBusDebugger.IsRecording, Is.True);

            EventBusDebugger.RemoveRecorder();
            Assert.That(EventBusDebugger.IsRecording, Is.False);
        }

        private static void AssertEntry(in EventBusDebugger.TraceEntry entry, EventBusDebugger.EntryKind kind, System.Type eventType, System.Type subjectType, int depth)
        {
            Assert.That(entry.Kind, Is.EqualTo(kind));
            Assert.That(entry.EventType, Is.EqualTo(eventType));
            Assert.That(entry.SubjectType, Is.EqualTo(subjectType));
            Assert.That(entry.Depth, Is.EqualTo(depth));
        }

        private static int FindBusId(EventBus bus, string uniqueName)
        {
            bus.SetDebugName(uniqueName);
            List<EventBusDebugger.BusInfo> buses = new();
            EventBusDebugger.GetBuses(buses);
            EventBusDebugger.BusInfo match = buses.FindLast(info => info.DisplayName == uniqueName);
            Assert.That(match.DisplayName, Is.EqualTo(uniqueName), "The bus did not register with the debugger.");
            return match.Id;
        }

        private static EventBusDebugger.BusInfo? GetBus(int busId)
        {
            List<EventBusDebugger.BusInfo> buses = new();
            EventBusDebugger.GetBuses(buses);
            int index = buses.FindIndex(info => info.Id == busId);
            return index >= 0 ? buses[index] : null;
        }

        private static long LatestSequence()
        {
            return EventBusDebugger.CopyEntriesSince(-1, new List<EventBusDebugger.TraceEntry>());
        }

        private static List<EventBusDebugger.TraceEntry> CopySince(long sequence)
        {
            List<EventBusDebugger.TraceEntry> entries = new();
            EventBusDebugger.CopyEntriesSince(sequence, entries);
            return entries;
        }
    }
}
