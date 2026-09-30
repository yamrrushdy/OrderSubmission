using OrderSubmission.Application.Notifications;

namespace OrderSubmission.UnitTests.Application;

public sealed class RetryBackoffTests
{
    private static readonly TimeSpan Initial = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Max = TimeSpan.FromMinutes(1);

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 60)]
    [InlineData(50, 60)]
    public void Delay_doubles_per_failure_up_to_the_cap(int failedAttempts, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetryBackoff.For(failedAttempts, Initial, Max));
    }
}
