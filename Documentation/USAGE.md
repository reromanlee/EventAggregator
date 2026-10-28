### Usage

Everything beyond the README quick start: where the bus lives, how listeners subscribe, and what the bus tells you when something is off. The importable **Getting Started** sample (Package Manager ▸ EventAggregator ▸ Samples) shows these patterns in a running scene.

### Owning the bus

Create one `EventBus` at your composition root, share it through `IEventBus`, and dispose it on shutdown. A bootstrap MonoBehaviour works:

```csharp
using reromanlee.EventAggregator;
using UnityEngine;

public sealed class GameBootstrap : MonoBehaviour
{
    private EventBus eventBus;

    private void Awake()
    {
        eventBus = new EventBus();
        eventBus.SetDebugName("Gameplay"); // Event Debugger label; stripped from player builds

        // Hand eventBus (as IEventBus) to your systems here, before they are enabled.
    }

    private void OnDestroy()
    {
        eventBus.Dispose();
    }
}
```

With a dependency injection container, register it as a singleton bound to its interfaces; containers that dispose `IDisposable` singletons also dispose the bus. For example, `builder.Register<EventBus>(Lifetime.Singleton).AsImplementedInterfaces();` in VContainer, or `Container.BindInterfacesTo<EventBus>().AsSingle();` in Zenject.

**Scoped buses are cheap.** A bus costs a few hundred bytes, so a bus per scene, per UI screen or per entity is a reasonable way to keep unrelated events apart.

### Defining events

Prefer small `readonly struct`s: they flow through the bus without boxing or garbage. Use a sealed class when the payload is large or shared.

```csharp
public readonly struct DamageDealt
{
    public readonly int TargetId;
    public readonly float Amount;

    public DamageDealt(int targetId, float amount)
    {
        TargetId = targetId;
        Amount = amount;
    }
}
```

The type argument selects the listeners, and the compiler infers it from the argument's static type: `Publish(baseTypedVariable)` publishes as the base type. Pass the type explicitly, `Publish<DamageDealt>(...)`, when in doubt.

### Listening

Implement `IEventListener<TEvent>` and pair `Subscribe` with `Unsubscribe`. For MonoBehaviours, `OnEnable`/`OnDisable` is the most robust pairing, because a disabled object then never handles events:

```csharp
public sealed class HealthBar : MonoBehaviour, IEventListener<DamageDealt>, IEventListener<HealthRestored>
{
    private IEventBus eventBus;

    public void Construct(IEventBus bus) => eventBus = bus;

    private void OnEnable()
    {
        // A class listening to several event types must name the type explicitly.
        eventBus.Subscribe<DamageDealt>(this);
        eventBus.Subscribe<HealthRestored>(this);
    }

    private void OnDisable()
    {
        eventBus.Unsubscribe<DamageDealt>(this);
        eventBus.Unsubscribe<HealthRestored>(this);
    }

    public void Handle(DamageDealt eventData) { /* ... */ }
    public void Handle(HealthRestored eventData) { /* ... */ }
}
```

Plain C# services subscribe when they start and unsubscribe in their own `Dispose`. Listeners must be classes.

### Publishing

```csharp
eventBus.Publish(new DamageDealt(targetId, 12.5f));
```

When building an event is expensive, typically a class payload with collections, check for listeners first; the check is lock-free and allocation-free:

```csharp
if (eventBus.HasListeners<LevelReport>())
{
    eventBus.Publish(new LevelReport(BuildStatistics()));
}
```

Handlers run synchronously on the publishing thread. When publishing from a background thread or a thread-pool `async` continuation, marshal Unity API work back to the main thread yourself.

### Debugging

Open **Window ▸ Analysis ▸ Event Debugger** and enter Play Mode. Name your buses with `SetDebugName` so the bus filter reads clearly. Toggle **Record** off while profiling to remove the editor tracing cost entirely.

The bus reports problems in the Console instead of failing silently:

| Message | Meaning |
|---|---|
| Listener exceptions (logged with their original stack trace) | A `Handle` call threw. Other listeners still received the event. |
| "… is already subscribed to …; duplicate subscription ignored." | The same instance subscribed twice to the same event type, usually a missing `Unsubscribe`. |
| "… was destroyed while still subscribed to … and has been unsubscribed automatically." | A destroyed MonoBehaviour or ScriptableObject never unsubscribed. Unsubscribe in `OnDisable` or `OnDestroy`. Editor and development builds only. |
| "… was published on a disposed EventBus and ignored." | An object outlived the bus's owner, usually a publish from `OnDisable`/`OnDestroy` during teardown. Logged once per bus. Editor and development builds only. |
| "Publish depth exceeded 64 while publishing …" | Listeners publish events that trigger each other forever. The rest of that chain was cancelled. Editor and development builds only. |

### Testing your code

Depend on `IEventBus` in your systems so tests can pass any implementation. A real `EventBus` is usually the simplest test double: create one per test, subscribe a recording listener, and dispose it in teardown.
