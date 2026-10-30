#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace reromanlee.EventAggregator
{
    /// <summary>
    /// Editor-only trace hub behind the Event Debugger window. Every <see cref="EventBus"/> registers here, and
    /// while at least one recorder (an open, recording window) is active, the publish path reports each publish
    /// and each delivery so the window can show what fired and where it went.
    /// </summary>
    /// <remarks>
    /// This type, and every call into it, is compiled only in the editor. With no recorder active the publish
    /// path pays a single flag check. All members are thread-safe; publishes may be reported from any thread.
    /// </remarks>
    internal static class EventBusDebugger
    {
        internal enum EntryKind : byte
        {
            /// <summary>Someone called Publish: the subject fired the event.</summary>
            Publish,

            /// <summary>One listener received the event: the event went to the subject.</summary>
            Delivery,
        }

        /// <summary>One immutable row of the trace. Stores types, not names, so recording never formats strings.</summary>
        internal readonly struct TraceEntry
        {
            /// <summary>Monotonically increasing and contiguous; never reset, so consumers can poll incrementally.</summary>
            public readonly long Sequence;

            /// <summary>Groups a root publish with every entry it caused, across buses.</summary>
            public readonly long ChainId;

            /// <summary>UTC ticks of when the entry was recorded.</summary>
            public readonly long TimestampTicks;

            public readonly Type EventType;

            /// <summary>The publisher for <see cref="EntryKind.Publish"/> (null when unknown), the receiving listener
            /// for <see cref="EntryKind.Delivery"/>.</summary>
            public readonly Type SubjectType;

            public readonly int BusId;

            /// <summary>Chain depth: 0 for a root publish, +1 per level of events firing events. Deliveries are one
            /// deeper than the publish that produced them.</summary>
            public readonly int Depth;

            public readonly EntryKind Kind;

            public TraceEntry(long sequence, long chainId, long timestampTicks, Type eventType, Type subjectType, int busId, int depth, EntryKind kind)
            {
                Sequence = sequence;
                ChainId = chainId;
                TimestampTicks = timestampTicks;
                EventType = eventType;
                SubjectType = subjectType;
                BusId = busId;
                Depth = depth;
                Kind = kind;
            }
        }

        /// <summary>A bus known to the debugger, as shown in the window's bus filter.</summary>
        internal readonly struct BusInfo
        {
            public readonly int Id;

            /// <summary>The name set via <see cref="EventBus.SetDebugName"/>, or "EventBus N".</summary>
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
            public string DefaultName;
            public string CustomName;
            public bool IsDisposed;
            public int EntryCount;

            // Weak, so the debugger never keeps a bus alive and can forget buses collected without Dispose.
            public WeakReference<EventBus> Bus;

            public string DisplayName => CustomName ?? DefaultName;

            public bool IsGone => IsDisposed || !Bus.TryGetTarget(out _);
        }

        /// <summary>Trace entries retained; the oldest are overwritten once full, so memory stays bounded.</summary>
        internal const int Capacity = 10000;

        private static readonly object gate = new();

        // Creation order for the filter, plus id lookup for the per-entry bookkeeping.
        private static readonly List<BusRecord> buses = new();
        private static readonly Dictionary<int, BusRecord> busesById = new();

        // Ring buffer, allocated on first record so sessions that never open the window pay nothing.
        private static TraceEntry[] entries;
        private static int oldestIndex;
        private static int entryCount;

        private static long nextSequence;
        private static long nextChain;
        private static int nextBusId;
        private static int version;
        private static int clearCount;
        private static int recorderCount;

        // The listener whose Handle is executing on this thread, so chained publishes can name their publisher.
        [ThreadStatic] private static Type currentHandler;

        // The chain the current publish on this thread belongs to; minted at every root publish.
        [ThreadStatic] private static long currentChain;

        /// <summary>True while at least one recorder is active. Checked by every publish in the editor.</summary>
        internal static bool IsRecording => Volatile.Read(ref recorderCount) > 0;

        /// <summary>Bumped on every change (entry, registration, rename, disposal, clear). Poll it cheaply and copy
        /// entries only when it changed.</summary>
        internal static int Version => Volatile.Read(ref version);

        /// <summary>Bumped by <see cref="Clear"/> so consumers know to discard rows they already built.</summary>
        internal static int ClearCount => Volatile.Read(ref clearCount);

        /// <summary>Starts recording for one consumer. Pair every call with <see cref="RemoveRecorder"/>.</summary>
        internal static void AddRecorder()
        {
            Interlocked.Increment(ref recorderCount);
        }

        /// <summary>Stops recording for one consumer. Never drops below zero, so unbalanced calls are harmless.</summary>
        internal static void RemoveRecorder()
        {
            int current;
            do
            {
                current = Volatile.Read(ref recorderCount);
                if (current == 0)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref recorderCount, current - 1, current) != current);
        }

        /// <summary>Registers a new bus and returns its debugger id.</summary>
        internal static int Register(EventBus bus)
        {
            lock (gate)
            {
                int id = ++nextBusId;

                // No '#' in the default name: editor menus parse a trailing "#N" as a Shift+N shortcut.
                BusRecord record = new() { Id = id, DefaultName = $"EventBus {id}", Bus = new WeakReference<EventBus>(bus) };
                buses.Add(record);
                busesById.Add(id, record);
                version++;
                return id;
            }
        }

        /// <summary>Marks a bus disposed. It stays listed while trace entries still reference it.</summary>
        internal static void Unregister(int busId)
        {
            lock (gate)
            {
                if (busesById.TryGetValue(busId, out BusRecord record) && !record.IsDisposed)
                {
                    record.IsDisposed = true;
                    PruneIfUnreferenced(record);
                    version++;
                }
            }
        }

        /// <summary>Gives a bus a custom display name; null or whitespace restores the default.</summary>
        internal static void SetBusName(int busId, string name)
        {
            lock (gate)
            {
                if (busesById.TryGetValue(busId, out BusRecord record))
                {
                    record.CustomName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
                    version++;
                }
            }
        }

        /// <summary>
        /// Fills <paramref name="target"/> with the known buses in creation order. Buses collected without being
        /// disposed are reported as disposed while their entries remain, and forgotten afterwards.
        /// </summary>
        internal static void GetBuses(List<BusInfo> target)
        {
            target.Clear();
            lock (gate)
            {
                for (int i = buses.Count - 1; i >= 0; i--)
                {
                    PruneIfUnreferenced(buses[i]);
                }
                foreach (BusRecord record in buses)
                {
                    target.Add(new BusInfo(record.Id, record.DisplayName, record.IsGone));
                }
            }
        }

        /// <summary>Records a publish. The publisher is the listener currently handling an event on this thread
        /// (a chained publish), or is resolved from the call stack for root publishes.</summary>
        internal static void RecordPublish(int busId, Type eventType, int depth)
        {
            if (depth == 0)
            {
                currentChain = Interlocked.Increment(ref nextChain);
            }
            Type publisher = currentHandler ?? ResolveExternalPublisher();
            Append(busId, EntryKind.Publish, depth, eventType, publisher);
        }

        /// <summary>Records one event reaching one listener.</summary>
        internal static void RecordDelivery(int busId, Type eventType, Type listenerType, int depth)
        {
            Append(busId, EntryKind.Delivery, depth, eventType, listenerType);
        }

        /// <summary>Marks <paramref name="listenerType"/> as the executing handler on this thread and returns the
        /// previous one, which must be restored via <see cref="EndHandler"/>.</summary>
        internal static Type BeginHandler(Type listenerType)
        {
            Type previous = currentHandler;
            currentHandler = listenerType;
            return previous;
        }

        internal static void EndHandler(Type previous)
        {
            currentHandler = previous;
        }

        /// <summary>Discards all trace entries and forgets disposed buses. Live buses stay registered.</summary>
        internal static void Clear()
        {
            lock (gate)
            {
                oldestIndex = 0;
                entryCount = 0;
                for (int i = buses.Count - 1; i >= 0; i--)
                {
                    BusRecord record = buses[i];
                    record.EntryCount = 0;
                    PruneIfUnreferenced(record);
                }
                clearCount++;
                version++;
            }
        }

        /// <summary>
        /// Appends every retained entry newer than <paramref name="sinceSequence"/> to <paramref name="target"/>, oldest
        /// first, and returns the newest sequence seen. Pass -1 to copy everything retained.
        /// </summary>
        internal static long CopyEntriesSince(long sinceSequence, List<TraceEntry> target)
        {
            lock (gate)
            {
                if (entryCount == 0)
                {
                    return sinceSequence;
                }

                long oldestSequence = entries[oldestIndex].Sequence;
                long newestSequence = oldestSequence + entryCount - 1;
                long firstSequence = Math.Max(sinceSequence + 1, oldestSequence);
                for (long sequence = firstSequence; sequence <= newestSequence; sequence++)
                {
                    target.Add(entries[(oldestIndex + (int)(sequence - oldestSequence)) % Capacity]);
                }
                return Math.Max(sinceSequence, newestSequence);
            }
        }

        private static void Append(int busId, EntryKind kind, int depth, Type eventType, Type subjectType)
        {
            long timestampTicks = DateTime.UtcNow.Ticks;
            lock (gate)
            {
                entries ??= new TraceEntry[Capacity];
                if (entryCount == Capacity)
                {
                    if (busesById.TryGetValue(entries[oldestIndex].BusId, out BusRecord evicted))
                    {
                        evicted.EntryCount--;
                        PruneIfUnreferenced(evicted);
                    }
                    oldestIndex = (oldestIndex + 1) % Capacity;
                    entryCount--;
                }

                entries[(oldestIndex + entryCount) % Capacity] =
                    new TraceEntry(nextSequence++, currentChain, timestampTicks, eventType, subjectType, busId, depth, kind);
                entryCount++;
                if (busesById.TryGetValue(busId, out BusRecord record))
                {
                    record.EntryCount++;
                }
                version++;
            }
        }

        /// <summary>Forgets a disposed or collected bus once no retained entry refers to it. Must be called under the gate.</summary>
        private static void PruneIfUnreferenced(BusRecord record)
        {
            if (record.EntryCount <= 0 && record.IsGone)
            {
                buses.Remove(record);
                busesById.Remove(record.Id);
            }
        }

        /// <summary>
        /// Resolves the type that called Publish by walking the stack past this package's frames. Compiler-generated
        /// types (async state machines, lambda closures) are unwrapped to the user type that declares them.
        /// </summary>
        private static Type ResolveExternalPublisher()
        {
            StackTrace stackTrace = new(2, false);
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                Type type = stackTrace.GetFrame(i)?.GetMethod()?.DeclaringType;
                if (type == null || type.Assembly == typeof(EventBusDebugger).Assembly)
                {
                    continue;
                }
                while (type.DeclaringType != null && type.Name.IndexOf('<') >= 0)
                {
                    type = type.DeclaringType;
                }
                return type;
            }
            return null;
        }
    }
}
#endif
