namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    // Gameplay events of one match. They travel on that match's own bus, which is disposed when the match ends.

    /// <summary>Published by <see cref="CombatSimulator"/> when an enemy appears.</summary>
    public readonly struct EnemySpawned
    {
        public readonly int EnemyId;

        public EnemySpawned(int enemyId)
        {
            EnemyId = enemyId;
        }
    }

    /// <summary>Published by <see cref="CombatSimulator"/> when an enemy is defeated.</summary>
    public readonly struct EnemyDefeated
    {
        public readonly int EnemyId;
        public readonly int Points;

        public EnemyDefeated(int enemyId, int points)
        {
            EnemyId = enemyId;
            Points = points;
        }
    }

    /// <summary>Published by <see cref="ScoreKeeper"/> while it handles <see cref="EnemyDefeated"/>: a chained event.</summary>
    public readonly struct ScoreChanged
    {
        public readonly int Score;
        public readonly int Delta;

        public ScoreChanged(int score, int delta)
        {
            Score = score;
            Delta = delta;
        }
    }

    /// <summary>Published by <see cref="MilestoneTracker"/> while it handles <see cref="ScoreChanged"/>: a chain two levels deep.</summary>
    public readonly struct MilestoneReached
    {
        public readonly int Milestone;

        public MilestoneReached(int milestone)
        {
            Milestone = milestone;
        }
    }
}
