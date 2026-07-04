# EventAggregator

A minimal, allocation-free, type-keyed publish/subscribe event bus for Unity. Producers publish plain C# objects or structs; listeners subscribe per event type. No reflection, no code generation, no dependencies — two interfaces and one class.

## Features

- **Zero-GC publishing** — `Publish` allocates no memory and takes no locks, so it is safe to call every frame on hot paths.
- **Thread-safe** — publish from any thread; subscriptions are safely serialized. Handlers may re-entrantly publish, subscribe, or unsubscribe without deadlocking.
- **Fault isolation** — a listener that throws is logged and skipped; the remaining listeners still receive the event.
- **AOT-friendly** — pure C#, no reflection or `Reflection.Emit`, safe under IL2CPP and aggressive managed code stripping on every Unity platform.
- **Debuggable failure modes** — in the editor and development builds, an infinite recursive publish loop (event A triggering B triggering A …) is reported as a clear error instead of a stack-overflow crash. The check compiles out of release builds.

## Installation

In Unity, open **Window ▸ Package Manager ▸ + ▸ Add package from git URL…** and enter:

```
https://github.com/reromanlee/EventAggregator.git
```

Or add it to `Packages/manifest.json` directly:

```json
{
  "dependencies": {
    "com.reromanlee.eventaggregator": "https://github.com/reromanlee/EventAggregator.git"
  }
}
```

Requires Unity 6000.0 or newer.

## Quick start

```csharp
using reromanlee.EventAggregator;
using UnityEngine;

// 1. Define an event. Structs are recommended: they flow through the bus
//    generically, with no boxing and no garbage.
public readonly struct ScoreChanged
{
    public readonly int NewScore;
    public ScoreChanged(int newScore) => NewScore = newScore;
}

// 2. Implement a listener and subscribe it. Always pair Subscribe with
//    Unsubscribe — the bus holds a strong reference to the listener.
public sealed class ScoreHud : MonoBehaviour, IEventListener<ScoreChanged>
{
    private IEventBus eventBus; // provide via your DI container, service locator, etc.

    private void OnEnable() => eventBus.Subscribe(this);
    private void OnDisable() => eventBus.Unsubscribe(this);

    public void Handle(ScoreChanged eventData)
    {
        Debug.Log($"Score is now {eventData.NewScore}");
    }
}

// 3. Publish from anywhere.
eventBus.Publish(new ScoreChanged(42));
```

Create one `EventBus` at your composition root and share it through `IEventBus`; call `Dispose` on shutdown to release all listeners. A single class can implement `IEventListener<T>` for several event types and subscribe to each.

## Delivery semantics

- Events are delivered **synchronously on the publishing thread**, in subscription order. `Publish` returns after the last listener runs.
- Event types are **matched exactly**: publishing a derived event does not notify listeners of its base type.
- Publishing an event that has no subscribers is a valid, silent no-op.
- A publish dispatches to the listeners subscribed **at the moment it starts**; listeners added or removed mid-publish (by a handler or another thread) take effect for subsequent publishes only.
- Duplicate subscriptions of the same listener instance to the same event type are ignored (with a warning), so a listener never handles one event twice.
- After `Dispose`, `Publish` and `Subscribe` throw `ObjectDisposedException`, while `Unsubscribe` is a harmless no-op — teardown code in `OnDestroy`/`OnDisable` stays safe regardless of destruction order.

## Performance

`Publish` is the designed-for hot path: one lock-free dictionary lookup by event type, then a plain array walk with one interface call per listener. It allocates **zero bytes** — listener lists are stored as immutable snapshot arrays that are only rebuilt when a listener is added or removed, so publishing produces no garbage regardless of frequency.

| Operation | Cost |
|---|---|
| `Publish` | O(number of listeners); lock-free; **0 B allocated** |
| `Subscribe` / `Unsubscribe` | O(number of listeners) under a short lock; allocates one small snapshot array |
| Struct events | No boxing anywhere on the publish path |

On a desktop .NET runtime, a publish measures in the tens of nanoseconds plus ~1–3 ns per listener; Unity's Mono and IL2CPP runtimes are somewhat slower per interface call, but the shape is the same — the cost of `Publish` is essentially the cost of your handlers.

## Memory footprint

The bus itself is small and reaches a fixed steady state (measured on 64-bit; Unity runtimes vary slightly):

| What | Approximate size |
|---|---|
| One `EventBus` instance | ~2.5 KB |
| Each distinct event type | ~200 B |
| Each subscribed listener | ~16 B (two references) |
| Each `Publish` | 0 B |

Subscribing and unsubscribing produce a small, short-lived snapshot array each time; these are rare, tiny, and collected in generation 0. Note that the bus holds **strong references** to listeners: a listener that never unsubscribes is kept alive (and keeps receiving events) for the lifetime of the bus — always unsubscribe when a listener's lifetime ends.

## Thread safety and async

All members are thread-safe. Publishing is lock-free; subscription changes serialize on a single internal lock that is never held while handlers execute, which is why handlers can freely publish or (un)subscribe re-entrantly. Handlers run synchronously on whichever thread called `Publish` — if that is a background thread, remember that most Unity APIs may only be touched from the main thread. `async` code can call `Publish` normally; the bus never blocks on or awaits handler code.

## Platform support

The implementation is pure C# against .NET Standard 2.1 (`ConcurrentDictionary`, `volatile`, `lock`, `[ThreadStatic]`) with no reflection, no runtime code generation, and no platform-specific calls. It behaves identically under Mono and IL2CPP on all Unity targets — desktop, mobile, consoles, and WebGL (where, without threads, the synchronization simply never contends) — and requires no link.xml or `[Preserve]` attributes under managed stripping.

## Recursive events

Handlers publishing further events is normal and supported. What is not survivable is an *infinite* publish cycle (A → B → A → …), which would crash a release player with an undebuggable stack overflow. In the editor and development builds the bus tracks publish depth per thread and reports an error naming the event type once a cycle exceeds depth 64; the check does not exist in release builds, so it costs nothing where performance matters. Fixing the cycle itself remains, deliberately, the developer's job.

## License

[MIT](LICENSE) © Roman Likhadievski
