using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>A listener of two event types. With several IEventListener implementations, name the type explicitly.</summary>
    public sealed class ScoreBoard : MonoBehaviour, IEventListener<ScoreChanged>, IEventListener<MilestoneReached>
    {
        private IEventBus eventBus;

        public void Construct(IEventBus bus)
        {
            eventBus = bus;
        }

        private void OnEnable()
        {
            eventBus.Subscribe<ScoreChanged>(this);
            eventBus.Subscribe<MilestoneReached>(this);
        }

        private void OnDisable()
        {
            eventBus.Unsubscribe<ScoreChanged>(this);
            eventBus.Unsubscribe<MilestoneReached>(this);
        }

        public void Handle(ScoreChanged eventData)
        {
            Debug.Log($"Score: {eventData.Score} (+{eventData.Delta})", this);
        }

        public void Handle(MilestoneReached eventData)
        {
            Debug.Log($"Milestone reached at {eventData.Score} points!", this);
        }
    }
}
