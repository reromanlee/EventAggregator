### Features

A detailed look at what EventAggregator guarantees and why. For setup and code, see [USAGE.md](USAGE.md).

### Zero-allocation, lock-free publishing

`Publish` finds an event type's listeners with a single array read (each event type gets a small integer index on first use) and walks them directly. It never takes a lock and never allocates, so it is safe to call every frame, from any thread, on hot paths. Struct events are passed generically all the way to `Handle`, so they are never boxed.

### Delivery semantics

1. Events are delivered **synchronously on the publishing thread**, in subscription order. `Publish` returns after the last listener ran.
2. Event types are **matched exactly** by the type argument of `Publish<TEvent>`, which the compiler infers from the argument's static type. Publishing a derived event does not reach listeners of its base type; `Publish<BaseEvent>(derived)` does.
3. Publishing an event nobody listens to is a valid, silent no-op.
4. A listener **unsubscribed during a publish stops immediately**, including the rest of the publish in progress. A disabled or destroyed object never handles one more event from the thread that called `Unsubscribe`.
5. A listener **subscribed during a publish** receives events from the next publish on, so chain reactions stay predictable.

### Subscriptions and identity

1. Listeners are identified **by reference**. `Equals` overrides (records, value-equality classes) cannot confuse duplicate detection or unsubscription.
2. Subscribing the same instance to the same event type twice is ignored with a warning, so a listener never handles one event twice.
3. Listeners must be classes. A struct listener is rejected with `ArgumentException`, because each call would box a new copy that could never be unsubscribed.
4. `IEventListener<in TEvent>` is contravariant: a listener of a base type can be subscribed explicitly to a derived event, e.g. `Subscribe<DerivedEvent>(baseListener)`.
5. Subscribing and unsubscribing are amortized O(1). Large groups switch to a hash lookup, and removals leave holes that are compacted in batches, so thousands of listeners can join or leave in one frame at linear cost.

### Lifetime and teardown

1. The bus holds **strong references** to listeners; always unsubscribe when a listener's lifetime ends.
2. A listener that is a **destroyed Unity object** (MonoBehaviour, ScriptableObject) but was never unsubscribed is skipped and unsubscribed automatically in every build. The editor and development builds log a warning naming the leaked listener and event type.
3. After `Dispose`, `Publish`, `Unsubscribe` and `HasListeners` are harmless no-ops, so Unity's non-deterministic destruction order can never crash teardown code. The editor and development builds log one warning per bus for the first publish after disposal. `Subscribe` after `Dispose` throws `ObjectDisposedException`, since that listener would never receive anything.
4. `Dispose` also stops any delivery still in progress, and disposing twice is allowed.

### Fault isolation

A listener that throws is reported through `Debug.LogException`, with the listener as the log context, and delivery continues with the remaining listeners. Double-clicking the Console entry opens the line that threw, and selecting it highlights the listener's GameObject.

### Recursion guard

Handlers publishing further events is normal. An infinite cycle (A → B → A → …) is not: a release player would crash with an undebuggable stack overflow. In the editor and development builds, the bus tracks publish depth per thread. Past depth 64 it logs one error naming the event type and cancels the rest of that event chain, so even fan-out loops (several listeners re-publishing) unwind in linear time instead of freezing. The guard is compiled out of release builds entirely.

### Threading and async

All members are thread-safe. Subscription changes serialize on one internal lock that is never held while handlers run, so handlers can publish, subscribe and unsubscribe re-entrantly without deadlocks. Handlers run on whichever thread called `Publish`; most Unity APIs must only be touched from the main thread. As with C# events, a publish already running on another thread may still deliver one last event to a listener that has just unsubscribed, so handlers invoked from background threads should tolerate that. `async` code can publish normally, since the bus never blocks on or awaits handler code.

### Platform support

The implementation is pure C# against .NET Standard 2.1 (`Interlocked`, `Volatile`, `lock`, `[ThreadStatic]`), with no reflection, runtime code generation or platform-specific calls. It behaves identically under Mono and IL2CPP on desktop, mobile, consoles and WebGL (where, without threads, the synchronization never contends), and needs no `link.xml` or `[Preserve]` attributes under managed code stripping.

### Event Debugger window

**Window ▸ Analysis ▸ Event Debugger** shows a live trace of every bus in the session. Each publish shows who fired which event and, unless the filter shows a single bus, names its bus at the end of the row, followed by a row per listener that received it. Events published from inside a handler are indented one level deeper, so chain reactions read as a tree, even when a handler publishes onto another bus, and each root publish starts a new block separated by a divider.

1. **Record** pauses or resumes capture. Buses report to the window only while it is open and recording, so a closed or paused window costs the publish path one flag check.
2. **Clear on Play** discards the trace when entering Play Mode, like the Console. **Clear** discards it at any time.
3. **Search** filters rows by event, publisher or listener name.
4. **Bus filter** shows one bus at a time. Buses appear as "EventBus 1", "EventBus 2", … in creation order, or under a name set with `SetDebugName`. Buses sharing a name are filtered together, and a `/` in a name groups buses into submenus. Disposed buses stay listed, marked as such, while the trace still holds their rows.
5. The list is virtualized: only visible rows exist, the newest 10,000 entries are kept, and the view follows new events until you scroll up, resuming once you scroll back to the bottom. Resizing or re-docking the window never stops it from following.
6. Colors follow the editor's Light or Dark theme.

All of the instrumentation behind the window is compiled only in the editor. Player builds, including development builds, contain none of it, and `SetDebugName` calls are stripped from them by the compiler.
