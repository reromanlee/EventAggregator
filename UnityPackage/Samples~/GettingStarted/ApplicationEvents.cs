namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    // Events are small immutable structs: they travel through the bus without boxing or garbage.
    // These ones travel on the long-lived "Application" bus, which outlives every match.

    /// <summary>Published by <see cref="MatchDirector"/> once a new match and its bus are ready.</summary>
    public readonly struct MatchStarted
    {
        public readonly int MatchNumber;

        public MatchStarted(int matchNumber)
        {
            MatchNumber = matchNumber;
        }
    }

    /// <summary>Published by <see cref="MatchDirector"/> after a match and its bus were torn down.</summary>
    public readonly struct MatchEnded
    {
        public readonly int MatchNumber;
        public readonly int FinalScore;

        public MatchEnded(int matchNumber, int finalScore)
        {
            MatchNumber = matchNumber;
            FinalScore = finalScore;
        }
    }
}
