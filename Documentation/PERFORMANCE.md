### Performance

How much EventAggregator costs, measured on real Unity runtimes. Numbers are best-of-five averages over one million publishes of a small struct event to listeners that add a field to a sum.

### Test setup

1. **IL2CPP:** Unity 6000.0.85f1 Windows x64 release player, managed stripping High, Intel Core i7-12700K.
2. **Mono:** Unity 2022.3.62f3 editor in Play Mode with release code optimization, same machine.
3. **Allocations and memory:** measured with exact byte counters on .NET 10 x64, whose object layout matches Unity's 64-bit runtimes; zero-allocation publishing is additionally verified in Unity by the test suite and in IL2CPP players.

Expect several times higher absolute numbers on mobile and WebGL hardware. The relative shape stays the same.

### Publishing

| Listeners | IL2CPP | Mono |
|---|---|---|
| 0 | 10 ns | 4 ns |
| 1 | 13 ns | 16 ns |
| 10 | 43 ns | 55 ns |
| 100 | 316 ns | 376 ns |

Publishing allocates **0 bytes** in every case and never takes a lock. The fixed cost is one array read to find the event type's listeners. Each listener then adds roughly 3 to 4 ns: one interface call plus two checks, whether it was unsubscribed mid-publish and whether it is a destroyed Unity object.

**One memory fence per publish.** IL2CPP compiles every volatile read into a full memory fence, which is a function call plus an `mfence` on x64 or a `dmb` on ARM. The publish path performs exactly one, regardless of listener count, and reads everything else in a consistent snapshot ordered by it. This matters most on ARM mobile devices, where fences are expensive.

### Subscribing and unsubscribing

| Burst into one event type | IL2CPP | Mono | Allocated |
|---|---|---|---|
| 1,000 subscribes | 0.31 ms | 0.20 ms | ~150 KB |
| 5,000 subscribes | 2.3 ms | 1.1 ms | ~1 MB |
| 5,000 unsubscribes | 0.58 ms | 0.34 ms | ~80 KB |

Both operations are amortized O(1). A subscribe allocates its subscription (40 B) and a small snapshot (32 B), plus an occasional doubling of the group's array. An unsubscribe allocates nothing except an occasional compaction once holes outnumber live listeners. Garbage therefore grows linearly with the number of listeners, which keeps large spawn waves safe on WebGL, where the garbage collector can only run between frames.

### Memory footprint

| What | Approximate size |
|---|---|
| An `EventBus` with one event type and one listener | ~270 B |
| Each additional event type on a bus | ~120 B plus one 8 B slot per event type index |
| Each listener in a small group (up to 8) | ~50 B |
| Each listener in a large group | ~70 B, including its hash lookup entry |
| Each `Publish` | 0 B |

An empty event type, one whose listeners all unsubscribed, releases its arrays and lookup, so idle groups hold almost nothing. The bus holds strong references to listeners until they unsubscribe; destroyed Unity objects are released automatically on the next publish of their event type.

### Operation costs

| Operation | Cost |
|---|---|
| `Publish` | O(listeners), lock-free, one memory fence, 0 B |
| `Subscribe` | Amortized O(1) under a short lock, ~72 B plus occasional growth |
| `Unsubscribe` | Amortized O(1) under a short lock, 0 B plus occasional compaction |
| `HasListeners` | O(1), lock-free, 0 B |
| Struct events | Never boxed |

### Editor overhead

With the Event Debugger window closed or paused, the editor adds one flag check per publish, so profiling in the editor matches player builds. While it is open and recording, each publish and delivery is appended to a bounded trace of 10,000 entries, and root publishes capture a stack trace to name their publisher. Pause recording while profiling. The editor and development builds also run the recursion guard, which costs a few thread-local reads per publish and is compiled out of release builds.
