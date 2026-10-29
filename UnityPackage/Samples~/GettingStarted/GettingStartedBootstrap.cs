using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>
    /// The composition root: owns the long-lived buses, builds the session and hands every component the buses it
    /// needs before it is enabled. Add it to an empty GameObject and enter Play Mode. In a real project this role
    /// usually belongs to your dependency injection container or game bootstrapper.
    /// </summary>
    public sealed class GettingStartedBootstrap : MonoBehaviour
    {
        private EventBus applicationBus;
        private EventBus interfaceBus;

        private void Awake()
        {
            // One bus per concern keeps unrelated listeners apart. MatchDirector adds a short-lived bus per match.
            applicationBus = new EventBus();
            applicationBus.SetDebugName("Application");
            interfaceBus = new EventBus();
            interfaceBus.SetDebugName("User Interface");

            GameObject session = ComponentHierarchy.CreateInactiveRoot("Session", transform);
            ComponentHierarchy.AddChild<Hud>(session, "Hud").Construct(interfaceBus);
            ComponentHierarchy.AddChild<SessionTracker>(session, "Session Tracker").Construct(applicationBus, interfaceBus);
            ComponentHierarchy.AddChild<MatchDirector>(session, "Match Director").Construct(applicationBus, interfaceBus);
            session.SetActive(true);
        }

        private void OnDestroy()
        {
            // Children may disable after this runs; their Unsubscribe calls are harmless no-ops once disposed.
            applicationBus.Dispose();
            interfaceBus.Dispose();
        }
    }
}
