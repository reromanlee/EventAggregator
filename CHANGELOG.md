# EventAggregator 1.0.0

### What's new

1. Type-keyed publish/subscribe event bus: `IEventBus`, `EventBus` and the contravariant `IEventListener<in TEvent>`, with one class able to listen to several event types.
2. Zero-allocation, lock-free `Publish` that finds listeners with a single per-type array read, safe to call every frame from any thread.
3. Amortized O(1) `Subscribe` and `Unsubscribe`, so thousands of listeners can join or leave in one frame at linear time and memory cost.
4. Listeners unsubscribed during a publish stop receiving immediately; listeners subscribed during a publish receive the next one.
5. Destroyed MonoBehaviour and ScriptableObject listeners are skipped and unsubscribed automatically, with a warning in the editor and development builds.
6. Teardown-safe disposal: `Publish`, `Unsubscribe` and `HasListeners` after `Dispose` are no-ops, and `Dispose` stops deliveries in progress.
7. Per-listener fault isolation through `Debug.LogException`, with clickable stack traces and the listener as log context.
8. `HasListeners<TEvent>()` to skip building events that nobody listens to.
9. Recursion guard in the editor and development builds that reports infinite publish loops and cancels the runaway chain, compiled out of release builds.
10. Event Debugger window (Window ▸ Analysis ▸ Event Debugger): virtualized live trace of publishes and deliveries as a chain tree, with Record, Clear on Play, search, per-bus filtering, `EventBus.SetDebugName` and Light/Dark theme support, costing one flag check per publish while closed.
11. Listeners are identified by reference, so `Equals` overrides cannot break unsubscription, and struct listeners are rejected.
12. Getting Started sample, Edit Mode and Play Mode test suites, and a GameCI workflow.
13. Unity 2022.3 LTS and Unity 6 support; safe under IL2CPP, managed code stripping and WebGL.

### Important

1. The package lives in the `UnityPackage/` folder of the repository, so git URL installs must end with `?path=/UnityPackage`.
