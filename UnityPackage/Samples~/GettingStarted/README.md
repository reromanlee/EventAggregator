# Getting Started

A minimal scene wired by a composition root, showing the patterns the package is built around.

## Run it

1. Open a new, empty scene.
2. Create an empty GameObject and add the **Getting Started Bootstrap** component.
3. Open **Window ▸ Analysis ▸ Event Debugger**, then enter Play Mode.

Every second the score rises and the Console logs it; every fifth point a milestone fires from inside a handler, so the Event Debugger shows it as a nested chain under the "Getting Started" bus.

## What to look at

| Script | Shows |
|---|---|
| `GameEvents.cs` | Events as small `readonly struct`s that flow through the bus without garbage. |
| `GettingStartedBootstrap.cs` | Owning the bus, injecting it before `OnEnable`, naming it for the debugger, disposing it on teardown. |
| `ScoreKeeper.cs` | Publishing. |
| `MilestoneTracker.cs` | Subscribing in `OnEnable`, unsubscribing in `OnDisable`, publishing a chained event from a handler. |
| `ScoreBoard.cs` | One class listening to two event types with explicit `Subscribe<T>(this)` calls. |
