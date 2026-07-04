# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-07-04

### Added

- `IEventBus` / `EventBus`: type-keyed publish/subscribe event bus with synchronous, subscription-order delivery.
- `IEventListener<in TEvent>`: contravariant listener interface; one class can listen to multiple event types.
- Zero-allocation, lock-free `Publish` built on copy-on-write listener snapshots — no GC pressure on the hot path, safe to call every frame from any thread.
- Per-listener exception isolation: a throwing listener is logged and skipped without interrupting delivery to the rest.
- Duplicate-subscription protection and null-argument validation on `Subscribe`/`Unsubscribe`.
- Recursive-publish depth guard in the editor and development builds that turns infinite publish loops into a clear error instead of a stack-overflow crash; compiled out of release builds.
- Disposal semantics designed for Unity teardown: `Unsubscribe` after `Dispose` is a safe no-op, while `Publish`/`Subscribe` throw `ObjectDisposedException`.
- Event Debugger window (Tools ▸ Event Debugger): live editor-only trace of publishes and deliveries per bus, with console-style timestamps, chain-depth indentation, per-instance filtering, sticky follow-scrolling, and `EventBus.SetDebugName` for readable instance names. No instrumentation is compiled into player builds.
- XML documentation for the entire public API.
