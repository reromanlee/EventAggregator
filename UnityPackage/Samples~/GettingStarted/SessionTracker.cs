using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>Bridges the application bus to the user interface bus: keeps the best score across matches and announces match results.</summary>
    public sealed class SessionTracker : MonoBehaviour, IEventListener<MatchStarted>, IEventListener<MatchEnded>
    {
        [SerializeField] private int bestScore;

        private IEventBus applicationBus;
        private IEventBus interfaceBus;

        public void Construct(IEventBus application, IEventBus userInterface)
        {
            applicationBus = application;
            interfaceBus = userInterface;
        }

        private void OnEnable()
        {
            applicationBus.Subscribe<MatchStarted>(this);
            applicationBus.Subscribe<MatchEnded>(this);
        }

        private void OnDisable()
        {
            applicationBus.Unsubscribe<MatchStarted>(this);
            applicationBus.Unsubscribe<MatchEnded>(this);
        }

        public void Handle(MatchStarted eventData)
        {
            interfaceBus.Publish(new NotificationRequested(NotificationKind.MatchStarted, eventData.MatchNumber));
        }

        public void Handle(MatchEnded eventData)
        {
            interfaceBus.Publish(new NotificationRequested(NotificationKind.MatchEnded, eventData.FinalScore));
            if (eventData.FinalScore > bestScore)
            {
                bestScore = eventData.FinalScore;
                interfaceBus.Publish(new NotificationRequested(NotificationKind.NewBestScore, bestScore));
            }
        }
    }
}
