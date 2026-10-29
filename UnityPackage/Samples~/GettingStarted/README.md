# Getting Started

A small session of back-to-back matches, wired by a composition root, showing the patterns the package is built around and how several buses look in the Event Debugger.

## Run it

1. Open a new, empty scene.
2. Create an empty GameObject and add the **Getting Started Bootstrap** component.
3. Open **Window ▸ Analysis ▸ Event Debugger**, then enter Play Mode.

An enemy spawns every 0.6 seconds and is defeated shortly after, the score climbs, and every 100 points a milestone fires. Each match lasts 15 seconds; then its bus and components are torn down and the next match starts on a fresh bus. The Console logs notifications, and the **Hud** object's Inspector shows the live score.

## The buses

| Bus | Lifetime | Events |
|---|---|---|
| **Application** | Whole session | `MatchStarted`, `MatchEnded` |
| **User Interface** | Whole session | `HudChanged`, `NotificationRequested` |
| **Match 1**, **Match 2**, … | One match, then disposed | `EnemySpawned`, `EnemyDefeated`, `ScoreChanged`, `MilestoneReached` |

## What to look at in the Event Debugger

1. **Bus names.** Every publish row names its bus on the right, and the bus menu filters the trace to one bus.
2. **Chains.** A defeat raises the score, which may cross a milestone: each event nests under the handler that published it.
3. **Chains across buses.** Presenters turn gameplay events into interface events, so one chain moves from a match bus to the User Interface bus.
4. **Scoped buses.** Finished matches stay listed as disposed until their rows leave the trace.

## What to look at in the code

| Script | Shows |
|---|---|
| `ApplicationEvents.cs`, `MatchEvents.cs`, `InterfaceEvents.cs` | Events as small `readonly struct`s grouped by the bus they travel on. |
| `GettingStartedBootstrap.cs` | Owning long-lived buses, naming them for the debugger, injecting them before `OnEnable`, disposing them on teardown. |
| `MatchDirector.cs` | Creating a bus per match and tearing it down safely: deactivate, destroy, dispose. |
| `ComponentHierarchy.cs` | Building objects under an inactive root, so no `OnEnable` runs before its buses are injected. |
| `CombatSimulator.cs` | Publishing. |
| `ScoreKeeper.cs`, `MilestoneTracker.cs` | Subscribing in `OnEnable`, unsubscribing in `OnDisable`, publishing a chained event from a handler. |
| `MatchPresenter.cs`, `SessionTracker.cs` | Bridging one bus to another, and one class listening to several event types with explicit `Subscribe<T>(this)` calls. |
| `Hud.cs` | The end of every chain: a listener that only consumes. |
