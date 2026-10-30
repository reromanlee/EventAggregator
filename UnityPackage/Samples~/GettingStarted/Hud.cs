using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>Stands in for the HUD: shows live values in its Inspector and logs notifications to the Console.</summary>
    public sealed class Hud : MonoBehaviour, IEventListener<HudChanged>, IEventListener<NotificationRequested>
    {
        [SerializeField] private int score;
        [SerializeField] private int enemiesAlive;

        private IEventBus eventBus;

        public void Construct(IEventBus bus)
        {
            eventBus = bus;
        }

        private void OnEnable()
        {
            eventBus.Subscribe<HudChanged>(this);
            eventBus.Subscribe<NotificationRequested>(this);
        }

        private void OnDisable()
        {
            eventBus.Unsubscribe<HudChanged>(this);
            eventBus.Unsubscribe<NotificationRequested>(this);
        }

        public void Handle(HudChanged eventData)
        {
            score = eventData.Score;
            enemiesAlive = eventData.EnemiesAlive;
        }

        public void Handle(NotificationRequested eventData)
        {
            string message = eventData.Kind switch
            {
                NotificationKind.MatchStarted => $"Match {eventData.Value} started.",
                NotificationKind.MilestoneReached => $"Milestone: {eventData.Value} points!",
                NotificationKind.MatchEnded => $"Match over with {eventData.Value} points.",
                NotificationKind.NewBestScore => $"New best score: {eventData.Value} points!",
                _ => eventData.Kind.ToString(),
            };
            Debug.Log(message, this);
        }
    }
}
