using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>Announces every milestone the score crosses, from inside its handler: the chain grows one level deeper.</summary>
    public sealed class MilestoneTracker : MonoBehaviour, IEventListener<ScoreChanged>
    {
        [SerializeField] private int milestoneStep = 100;

        private IEventBus eventBus;

        public void Construct(IEventBus bus)
        {
            eventBus = bus;
        }

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
            int previousScore = eventData.Score - eventData.Delta;
            int milestones = eventData.Score / milestoneStep;
            if (milestones > previousScore / milestoneStep)
            {
                eventBus.Publish(new MilestoneReached(milestones * milestoneStep));
            }
        }
    }
}
