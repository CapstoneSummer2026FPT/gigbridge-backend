using Microsoft.Extensions.Options;

namespace Application.Common.Options;

public sealed class DeliveryOutboxOptionsValidator : IValidateOptions<DeliveryOutboxOptions>
{
    private const int SupavisorPoolSizeCeiling = 15;
    private const int ReservedForNonOutboxConnections = 5;

    public ValidateOptionsResult Validate(string? name, DeliveryOutboxOptions options)
    {
        var failures = new List<string>();

        CheckRange(failures, nameof(options.RealtimePollMilliseconds), options.RealtimePollMilliseconds, 50, 1_000);
        CheckRange(failures, nameof(options.EmailPollMilliseconds), options.EmailPollMilliseconds, 250, 60_000);
        CheckRange(failures, nameof(options.BatchSize), options.BatchSize, 1, 100);
        CheckRange(failures, nameof(options.RealtimeMaxConcurrency), options.RealtimeMaxConcurrency, 1, 32);
        CheckRange(failures, nameof(options.EmailMaxConcurrency), options.EmailMaxConcurrency, 1, 16);
        CheckRange(
            failures,
            nameof(options.MaxConcurrentDbConnections),
            options.MaxConcurrentDbConnections,
            1,
            SupavisorPoolSizeCeiling);
        CheckRange(failures, nameof(options.LeaseMinutes), options.LeaseMinutes, 1, 120);
        CheckRange(failures, nameof(options.LeaseRecoveryIntervalSeconds), options.LeaseRecoveryIntervalSeconds, 10, 600);
        CheckRange(failures, nameof(options.BackfillPageSize), options.BackfillPageSize, 1, 200);
        CheckRange(failures, nameof(options.DeliveredPayloadRetentionDays), options.DeliveredPayloadRetentionDays, 1, 365);
        CheckRange(failures, nameof(options.RetentionIntervalMinutes), options.RetentionIntervalMinutes, 5, 1_440);
        CheckRange(failures, nameof(options.RetentionBatchSize), options.RetentionBatchSize, 100, 10_000);

        var availableForOutbox = SupavisorPoolSizeCeiling - ReservedForNonOutboxConnections;
        if (options.MaxConcurrentDbConnections > availableForOutbox)
        {
            failures.Add(
                $"{nameof(options.MaxConcurrentDbConnections)}={options.MaxConcurrentDbConnections} exceeds the " +
                $"outbox budget of {availableForOutbox}: Supavisor pool_size is {SupavisorPoolSizeCeiling}, " +
                $"with {ReservedForNonOutboxConnections} connections reserved for HTTP traffic, other workers, " +
                "and operational clients. This limit is per worker process; lower it further when multiple " +
                "worker processes share the same Supavisor pool.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void CheckRange(
        ICollection<string> failures,
        string name,
        int value,
        int minimum,
        int maximum)
    {
        if (value < minimum || value > maximum)
        {
            failures.Add($"{name}={value} is outside the allowed range [{minimum}, {maximum}].");
        }
    }
}
