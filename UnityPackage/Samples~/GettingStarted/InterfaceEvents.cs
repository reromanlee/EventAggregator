namespace reromanlee.EventAggregator.Samples.GettingStarted
{
    // Presentation events on the long-lived "User Interface" bus. Gameplay code never publishes them directly:
    // presenters translate gameplay events into these, so the interface can change without touching gameplay.

    /// <summary>What a <see cref="NotificationRequested"/> announces.</summary>
    public enum NotificationKind
    {
        MatchStarted,
        MilestoneReached,
        MatchEnded,
        NewBestScore,
    }

    /// <summary>Published by <see cref="MatchPresenter"/> whenever a value shown on the HUD changes.</summary>
    public readonly struct HudChanged
    {
        public readonly int Score;
        public readonly int EnemiesAlive;

        public HudChanged(int score, int enemiesAlive)
        {
            Score = score;
            EnemiesAlive = enemiesAlive;
        }
    }

    /// <summary>Published by presenters to show a short message. Carries a kind and a number rather than text, so publishing formats nothing.</summary>
    public readonly struct NotificationRequested
    {
        public readonly NotificationKind Kind;
        public readonly int Value;

        public NotificationRequested(NotificationKind kind, int value)
        {
            Kind = kind;
            Value = value;
        }
    }
}
