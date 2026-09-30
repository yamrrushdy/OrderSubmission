namespace OrderSubmission.Application.Notifications;

public static class RetryBackoff
{
    public static TimeSpan For(int failedAttempts, TimeSpan initialDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempts, 1);

        var exponent = Math.Min(failedAttempts - 1, 20);
        var delay = initialDelay * Math.Pow(2, exponent);

        return delay < maxDelay ? delay : maxDelay;
    }
}
