using Microsoft.Extensions.Options;
using OrderSubmission.Domain.Common;

namespace OrderSubmission.Infrastructure.Notifications;

public sealed record FaultSettings(bool SimulateOutage, int FailFirstAttempts, double FailureRate)
{
    public const int MaxFailFirstAttempts = 10;

    public static readonly FaultSettings None = new(false, 0, 0);

    public Result Validate()
    {
        var failures = new Dictionary<string, string[]>();

        if (FailFirstAttempts is < 0 or > MaxFailFirstAttempts)
        {
            failures[nameof(FailFirstAttempts)] = [$"Must be between 0 and {MaxFailFirstAttempts}."];
        }

        if (double.IsNaN(FailureRate) || FailureRate is < 0 or > 1)
        {
            failures[nameof(FailureRate)] = ["Must be between 0 and 1."];
        }

        return failures.Count == 0 ? Result.Success() : new ValidationError(failures);
    }
}

public sealed class NotificationFaultSimulator(IOptions<FakeNotificationOptions> options)
{
    private FaultSettings _settings = new(
        options.Value.SimulateOutage,
        options.Value.FailFirstAttempts,
        options.Value.FailureRate);

    public FaultSettings Current => Volatile.Read(ref _settings);

    public Result Apply(FaultSettings settings)
    {
        var validation = settings.Validate();
        if (validation.IsSuccess)
        {
            Volatile.Write(ref _settings, settings);
        }

        return validation;
    }

    public void Reset() => Volatile.Write(ref _settings, FaultSettings.None);

    public string? FailureReasonFor(int attempt)
    {
        var settings = Current;

        if (settings.SimulateOutage)
        {
            return "Notification provider unavailable (simulated outage).";
        }

        if (attempt <= settings.FailFirstAttempts)
        {
            return $"Notification provider rejected attempt {attempt} (simulated transient failure).";
        }

        if (settings.FailureRate > 0 && Random.Shared.NextDouble() < settings.FailureRate)
        {
            return "Notification provider timed out (simulated random failure).";
        }

        return null;
    }
}
