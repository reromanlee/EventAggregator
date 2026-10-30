using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>
    /// Bridges a match bus to the user interface bus: listens to gameplay events and publishes what the HUD should
    /// show. Gameplay never references the interface, and the Event Debugger shows each translation as a chain
    /// that crosses from one bus into another.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour,
        IEventListener<EnemySpawned>, IEventListener<EnemyDefeated>, IEventListener<ScoreChanged>, IEventListener<MilestoneReached>
    {
        private IEventBus matchBus;
        private IEventBus interfaceBus;
        private int score;
        private int enemiesAlive;

        public void Construct(IEventBus match, IEventBus userInterface)
        {
            matchBus = match;
            interfaceBus = userInterface;
        }

        // A class listening to several event types names each type explicitly.
        private void OnEnable()
        {
            matchBus.Subscribe<EnemySpawned>(this);
            matchBus.Subscribe<EnemyDefeated>(this);
            matchBus.Subscribe<ScoreChanged>(this);
            matchBus.Subscribe<MilestoneReached>(this);
            PublishHud();
        }

        private void OnDisable()
        {
            matchBus.Unsubscribe<EnemySpawned>(this);
            matchBus.Unsubscribe<EnemyDefeated>(this);
            matchBus.Unsubscribe<ScoreChanged>(this);
            matchBus.Unsubscribe<MilestoneReached>(this);
        }

        public void Handle(EnemySpawned eventData)
        {
            enemiesAlive++;
            PublishHud();
        }

        public void Handle(EnemyDefeated eventData)
        {
            enemiesAlive--;
            PublishHud();
        }

        public void Handle(ScoreChanged eventData)
        {
            score = eventData.Score;
            PublishHud();
        }

        public void Handle(MilestoneReached eventData)
        {
            interfaceBus.Publish(new NotificationRequested(NotificationKind.MilestoneReached, eventData.Milestone));
        }

        private void PublishHud()
        {
            interfaceBus.Publish(new HudChanged(score, enemiesAlive));
        }
    }
}
