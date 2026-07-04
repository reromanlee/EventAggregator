#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace reromanlee.EventAggregator
{
    /// <summary>
    /// Editor-only trace hub behind the Event Debugger window (Tools ▸ Event Debugger).
    /// Every <see cref="EventBus"/> registers itself here, and the publish path reports each
    /// publish and each delivery so the window can show what fired and where it went.
    /// </summary>
    /// <remarks>
    /// This entire type — and every call into it — is compiled only in the editor, so player
    /// builds (including development builds) carry none of this code and none of its cost.
    /// All members are thread-safe; publishes may be reported from any thread.
    /// </remarks>
    internal static class EventBusDebugger
    {
        internal enum EntryKind : byte
        {
            /// <summary>Someone called Publish: SourceName fired EventName.</summary>
            Publish,
            /// <summary>One listener received the event: EventName went to TargetName.</summary>
            Delivery,
        }

        /// <summary>One immutable row of the trace.</summary>
        internal readonly struct TraceEntry
        {
            /// <summary>Monotonically increasing id; never reset, so consumers can poll incrementally.</summary>
            public readonly long Sequence;
            public readonly int BusId;
            public readonly EntryKind Kind;
            /// <summary>Chain depth: 0 for a root publish, +1 for every level of events firing events.
            /// Deliveries are recorded one deeper than the publish that produced them.</summary>
            public readonly int Depth;
            public readonly string EventName;
            /// <summary>Who published — a listener type name for chained events, the caller's type name
            /// (resolved from the stack) for root publishes. Null for <see cref="EntryKind.Delivery"/>.</summary>
            public readonly string SourceName;
            /// <summary>The receiving listener's type name. Null for <see cref="EntryKind.Publish"/>.</summary>
            public readonly string TargetName;

            public TraceEntry(long sequence, int busId, EntryKind kind, int depth, string eventName, string sourceName, string targetName)
            {
                Sequence = sequence;
                BusId = busId;
                Kind = kind;
                Depth = depth;
                EventName = eventName;
                SourceName = sourceName;
                TargetName = targetName;
            }
        }

        /// <summary>A bus known to the debugger. Records are kept after disposal so the trace history stays attributable.</summary>
        internal readonly struct BusInfo
        {
            public readonly int Id;
            /// <summary>Base display name without any status decoration: the custom name set via
            /// <see cref="EventBus.SetDebugName"/>, or "EventBus #id".</summary>
            public readonly string DisplayName;
            public readonly bool IsDisposed;

            public BusInfo(int id, string displayName, bool isDisposed)
            {
                Id = id;
                DisplayName = displayName;
                IsDisposed = isDisposed;
            }
        }

        private sealed class BusRecord
        {
            public int Id;
            public string CustomName;
            public bool Disposed;
        }

        // Bounded so a publish-heavy play session cannot grow editor memory without limit.
        private const int MaxEntries = 2000;
        private const int TrimChunk = 500;

        private static readonly object gate = new();
        private static readonly List<TraceEntry> entries = new();
        private static readonly List<BusRecord> buses = new();
        private static long nextSequence;
        private static int nextBusId;
        private static int version;
        private static int clearCount;

        // The type name of the listener whose Handle is currently executing on this thread.
        // Lets chained publishes attribute themselves to the listener that fired them.
        [ThreadStatic] private static string currentHandler;

        /// <summary>Bumped on every mutation (entry, registration, rename, clear). Poll this cheaply
        /// and only copy entries when it changed.</summary>
        internal static int Version => Volatile.Read(ref version);

        /// <summary>Bumped by <see cref="Clear"/> so consumers know to discard rows they already built.</summary>
        internal static int ClearCount => Volatile.Read(ref clearCount);

        /// <summary>Registers a new bus and returns its debugger id.</summary>
        internal static int Register()
        {
            lock (gate)
            {
                BusRecord record = new() { Id = ++nextBusId };
                buses.Add(record);
                version++;
                return record.Id;
            }
        }

        /// <summary>Marks a bus disposed. Its record and trace entries remain visible.</summary>
        internal static void Unregister(int busId)
        {
            lock (gate)
            {
                BusRecord record = Find(busId);
                if (record != null && !record.Disposed)
                {
                    record.Disposed = true;
                    version++;
                }
            }
        }

        /// <summary>Gives a bus a human-readable name in place of the default "EventBus #id".</summary>
        internal static void SetBusName(int busId, string name)
        {
            lock (gate)
            {
                BusRecord record = Find(busId);
                if (record != null)
                {
                    record.CustomName = name;
                    version++;
                }
            }
        }

        /// <summary>Fills <paramref name="target"/> with all known buses, in creation order.</summary>
        internal static void GetBuses(List<BusInfo> target)
        {
            target.Clear();
            lock (gate)
            {
                foreach (BusRecord record in buses)
                {
                    target.Add(new BusInfo(record.Id, record.CustomName ?? $"EventBus #{record.Id}", record.Disposed));
                }
            }
        }

        /// <summary>Records a publish. The publisher is the listener currently handling an event on
        /// this thread (chained publish), or resolved from the call stack for root publishes.</summary>
        internal static void OnPublish(int busId, Type eventType, int depth)
        {
            string publisher = currentHandler ?? ResolveExternalPublisher();
            Append(busId, EntryKind.Publish, depth, eventType.Name, publisher, null);
        }

        /// <summary>Records one event reaching one listener.</summary>
        internal static void OnDelivery(int busId, Type eventType, string listenerName, int depth)
        {
            Append(busId, EntryKind.Delivery, depth, eventType.Name, null, listenerName);
        }

        /// <summary>Marks <paramref name="listenerName"/> as the executing handler on this thread and
        /// returns the previous value, which must be restored via <see cref="EndHandler"/>.</summary>
        internal static string BeginHandler(string listenerName)
        {
            string previous = currentHandler;
            currentHandler = listenerName;
            return previous;
        }

        internal static void EndHandler(string previous)
        {
            currentHandler = previous;
        }

        /// <summary>Discards all trace entries. Known buses stay registered.</summary>
        internal static void Clear()
        {
            lock (gate)
            {
                entries.Clear();
                clearCount++;
                version++;
            }
        }

        /// <summary>
        /// Appends every entry newer than <paramref name="sinceSequence"/> to <paramref name="target"/>
        /// and returns the newest sequence seen (or <paramref name="sinceSequence"/> when none).
        /// Pass -1 to copy everything currently retained.
        /// </summary>
        internal static long CopyEntriesSince(long sinceSequence, List<TraceEntry> target)
        {
            lock (gate)
            {
                int start = entries.Count;
                while (start > 0 && entries[start - 1].Sequence > sinceSequence)
                {
                    start--;
                }
                for (int i = start; i < entries.Count; i++)
                {
                    target.Add(entries[i]);
                }
                return entries.Count > 0 ? entries[entries.Count - 1].Sequence : sinceSequence;
            }
        }

        private static BusRecord Find(int busId)
        {
            foreach (BusRecord record in buses)
            {
                if (record.Id == busId)
                {
                    return record;
                }
            }
            return null;
        }

        private static void Append(int busId, EntryKind kind, int depth, string eventName, string sourceName, string targetName)
        {
            lock (gate)
            {
                if (entries.Count >= MaxEntries)
                {
                    entries.RemoveRange(0, TrimChunk);
                }
                entries.Add(new TraceEntry(nextSequence++, busId, kind, depth, eventName, sourceName, targetName));
                version++;
            }
        }

        /// <summary>
        /// Resolves the type that called Publish by walking the stack past this package's frames.
        /// Compiler-generated types (async state machines, lambda display classes) are unwrapped to
        /// the user type that declares them.
        /// </summary>
        private static string ResolveExternalPublisher()
        {
            StackTrace stackTrace = new(2, false);
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                Type type = stackTrace.GetFrame(i).GetMethod()?.DeclaringType;
                if (type == null || type.Assembly == typeof(EventBusDebugger).Assembly)
                {
                    continue;
                }
                while (type.DeclaringType != null && type.Name.IndexOf('<') >= 0)
                {
                    type = type.DeclaringType;
                }
                return type.Name;
            }
            return "Unknown";
        }
    }
}
#endif
