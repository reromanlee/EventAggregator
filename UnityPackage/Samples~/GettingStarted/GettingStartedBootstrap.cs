using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>
    /// The composition root: owns the bus, builds the scene and hands every component the bus before it is
    /// enabled. Add it to an empty GameObject and enter Play Mode. In a real project this role usually belongs to
    /// your dependency injection container or game bootstrapper.
    /// </summary>
    public sealed class GettingStartedBootstrap : MonoBehaviour
    {
        private EventBus eventBus;

        private void Awake()
        {
            eventBus = new EventBus();
            eventBus.SetDebugName("Getting Started");

            Activate(Create<ScoreBoard>("Score Board"), component => component.Construct(eventBus));
            Activate(Create<MilestoneTracker>("Milestone Tracker"), component => component.Construct(eventBus));
            Activate(Create<ScoreKeeper>("Score Keeper"), component => component.Construct(eventBus));
        }

        private void OnDestroy()
        {
            // Children may disable after this runs; their Unsubscribe calls are harmless no-ops once disposed.
            eventBus.Dispose();
        }

        /// <summary>Creates the component on an inactive child, so OnEnable cannot run before the bus is injected.</summary>
        private T Create<T>(string objectName) where T : Component
        {
            GameObject child = new(objectName);
            child.SetActive(false);
            child.transform.SetParent(transform, false);
            return child.AddComponent<T>();
        }

        private static void Activate<T>(T component, System.Action<T> construct) where T : Component
        {
            construct(component);
            component.gameObject.SetActive(true);
        }
    }
}
