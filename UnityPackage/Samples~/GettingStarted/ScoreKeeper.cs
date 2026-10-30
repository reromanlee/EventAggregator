using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>A listener that publishes from inside its handler: each defeat becomes a score change, nested under it in the Event Debugger.</summary>
    public sealed class ScoreKeeper : MonoBehaviour, IEventListener<EnemyDefeated>
    {
        private IEventBus eventBus;
        private int score;

        public int Score => score;

        /// <summary>Receives the bus from its composition root before the component is enabled.</summary>
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

        public void Handle(EnemyDefeated eventData)
        {
            score += eventData.Points;
            eventBus.Publish(new ScoreChanged(score, eventData.Points));
        }
    }
}
