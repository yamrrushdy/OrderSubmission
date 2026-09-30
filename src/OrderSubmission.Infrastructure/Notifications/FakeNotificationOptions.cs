namespace OrderSubmission.Infrastructure.Notifications;

public sealed class FakeNotificationOptions
{
    public const string SectionName = "Notifications:Fake";

    public bool SimulateOutage { get; set; }

    public int FailFirstAttempts { get; set; }

    public double FailureRate { get; set; }

    public TimeSpan Latency { get; set; } = TimeSpan.FromMilliseconds(200);
}
