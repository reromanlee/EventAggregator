#if UNITY_EDITOR
using System.Runtime.CompilerServices;

// Grants the editor assembly access to the editor-only EventBusDebugger internals.
// Compiled out of player builds together with everything it exposes.
[assembly: InternalsVisibleTo("reromanlee.EventAggregator.Editor")]
#endif
