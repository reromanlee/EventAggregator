using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>A publisher: raises the score on a fixed interval and announces every change.</summary>
    public sealed class ScoreKeeper : MonoBehaviour
    {
        [SerializeField] private float tickInterval = 1f;
        [SerializeField] private int pointsPerTick = 1;

        private IEventBus eventBus;
        private int score;
        private float nextTickTime;

        /// <summary>Receives the bus from the composition root before the component is enabled.</summary>
        public void Construct(IEventBus bus)
        {
            eventBus = bus;
        }

        private void Update()
        {
            if (Time.time < nextTickTime)
            {
                return;
            }
            nextTickTime = Time.time + tickInterval;

            score += pointsPerTick;
            eventBus.Publish(new ScoreChanged(score, pointsPerTick));
        }
    }
}
