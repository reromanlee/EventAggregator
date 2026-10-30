### Testing

The package ships its test suite in `UnityPackage/Tests`. It compiles only in projects that opt in, so it costs nothing otherwise.

### What is covered

1. **Runtime** (`Tests/Runtime`, Play Mode and players): delivery order and exact type matching, contravariance, re-entrant publishing, unsubscribing and subscribing mid-publish, mass churn during a publish, reference identity, duplicate and struct listeners, large groups across compaction, dispose semantics, destroyed MonoBehaviour and ScriptableObject listeners, recursion and fan-out loops, multi-threaded publishing under subscription churn, and zero-allocation publishing.
2. **Editor** (`Tests/Editor`, Edit Mode): the Event Debugger trace hub (recording gate, chain depths and publishers, bus naming and pruning, bounded capacity) and a smoke test that opens the window and checks that it lists published events.

For 1.0.0, both suites passed in the editor on Unity 2022.3.62f3 and Unity 6000.0.85f1. The core guarantees were also verified in IL2CPP Windows release and development players with managed stripping set to High: immediate unsubscription, ordering across compaction, reference identity, destroyed-listener pruning, dispose semantics, fault isolation, fan-out loop cancellation, concurrent publishing under churn, and zero-allocation publishing.

### Running the tests locally

Add the package to `testables` in your project's `Packages/manifest.json`, then open **Window ▸ General ▸ Test Runner**:

```json
{
  "dependencies": {
    "com.reromanlee.eventaggregator": "https://github.com/reromanlee/EventAggregator.git?path=/UnityPackage"
  },
  "testables": [
    "com.reromanlee.eventaggregator"
  ]
}
```

Run Edit Mode and Play Mode tests in the editor, or pick a player platform in the Test Runner to run the runtime suite on a device. Close the Event Debugger window before running the allocation tests, since a recording window traces every publish by design.

### Continuous integration

`.github/workflows/tests.yml` runs both suites with [GameCI](https://game.ci) on every push to `main` and every pull request, on Unity 2022.3 and Unity 6. It needs three repository secrets: `UNITY_LICENSE` (the contents of your `.ulf` license file), `UNITY_EMAIL` and `UNITY_PASSWORD`. Until they exist, and for pull requests from forks, the jobs pass with a notice instead of running.
