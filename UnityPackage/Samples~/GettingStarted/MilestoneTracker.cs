using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>A listener that publishes from inside its handler: the Event Debugger shows it as a nested chain.</summary>
    public sealed class MilestoneTracker : MonoBehaviour, IEventListener<ScoreChanged>
    {
        [SerializeField] private int milestoneStep = 5;

        private IEventBus eventBus;

        public void Construct(IEventBus bus)
        {
            eventBus = bus;
        }

        // Subscribe and unsubscribe symmetrically, so a disabled or destroyed object never keeps listening.
        private void OnEnable()
        {
            eventBus.Subscribe(this);
        }

        private void OnDisable()
        {
            eventBus.Unsubscribe(this);
        }

        public void Handle(ScoreChanged eventData)
        {
            if (eventData.Score % milestoneStep == 0)
            {
                eventBus.Publish(new MilestoneReached(eventData.Score));
            }
        }
    }
}
