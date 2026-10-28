namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    // Events are small immutable structs: they travel through the bus without boxing or garbage.

    /// <summary>Published by <see cref="ScoreKeeper"/> whenever the score changes.</summary>
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

    /// <summary>Published by <see cref="MilestoneTracker"/> while it handles <see cref="ScoreChanged"/> — a chained event.</summary>
    public readonly struct MilestoneReached
    {
        public readonly int Score;

        public MilestoneReached(int score)
        {
            Score = score;
        }
    }
}
