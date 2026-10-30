#if UNITY_EDITOR
using System.Runtime.CompilerServices;

// Grants the editor and test assemblies access to the editor-only EventBusDebugger internals.
// Compiled out of player builds together with everything it exposes.
[assembly: InternalsVisibleTo("reromanlee.EventAggregator.Editor")]
[assembly: InternalsVisibleTo("reromanlee.EventAggregator.Editor.Tests")]
[assembly: InternalsVisibleTo("reromanlee.EventAggregator.Tests")]
#endif
