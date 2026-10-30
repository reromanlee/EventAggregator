using UnityEngine;

namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    /// <summary>
    /// Runs matches back to back. Each match gets its own bus and its own components, and both are torn down when
    /// it ends, so nothing from one match can leak into the next. The Event Debugger lists every match bus and
    /// marks finished ones as disposed.
    /// </summary>
    public sealed class MatchDirector : MonoBehaviour
    {
        [SerializeField] private float matchDuration = 15f;
        [SerializeField] private float breakDuration = 3f;

        private IEventBus applicationBus;
        private IEventBus interfaceBus;
        private EventBus matchBus;
        private GameObject matchRoot;
        private ScoreKeeper scoreKeeper;
        private int matchNumber;
        private float nextTransitionTime;

        public void Construct(IEventBus application, IEventBus userInterface)
        {
            applicationBus = application;
            interfaceBus = userInterface;
        }

        private void Update()
        {
            if (Time.time < nextTransitionTime)
            {
                return;
            }

            if (matchBus == null)
            {
                StartMatch();
            }
            else
            {
                EndMatch();
            }
        }

        private void OnDestroy()
        {
            matchBus?.Dispose();
        }

        private void StartMatch()
        {
            matchNumber++;
            matchBus = new EventBus();
            matchBus.SetDebugName($"Match {matchNumber}");

            matchRoot = ComponentHierarchy.CreateInactiveRoot($"Match {matchNumber}", transform);
            scoreKeeper = ComponentHierarchy.AddChild<ScoreKeeper>(matchRoot, "Score Keeper");
            scoreKeeper.Construct(matchBus);
            ComponentHierarchy.AddChild<MilestoneTracker>(matchRoot, "Milestone Tracker").Construct(matchBus);
            ComponentHierarchy.AddChild<MatchPresenter>(matchRoot, "Match Presenter").Construct(matchBus, interfaceBus);
            ComponentHierarchy.AddChild<CombatSimulator>(matchRoot, "Combat Simulator").Construct(matchBus);
            matchRoot.SetActive(true);

            applicationBus.Publish(new MatchStarted(matchNumber));
            nextTransitionTime = Time.time + matchDuration;
        }

        private void EndMatch()
        {
            int finalScore = scoreKeeper.Score;

            // Deactivating first runs every OnDisable right now, so the match's components unsubscribe and stop
            // updating before their bus is disposed.
            matchRoot.SetActive(false);
            Destroy(matchRoot);
            matchBus.Dispose();
            matchBus = null;
            matchRoot = null;
            scoreKeeper = null;

            applicationBus.Publish(new MatchEnded(matchNumber, finalScore));
            nextTransitionTime = Time.time + breakDuration;
        }
    }
}
