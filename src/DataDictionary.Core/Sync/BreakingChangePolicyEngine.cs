using DataDictionary.Abstractions.Configuration;
using DataDictionary.Abstractions.Sync;

namespace DataDictionary.Core.Sync;

/// <summary>
/// Applies the configured <see cref="OnBreakingChange"/> policy to a
/// <see cref="SynchronizationOutcome"/> produced by <see cref="DictionaryDiffEngine"/>,
/// deciding whether the breaking changes it carries abort the boot, are logged and
/// skipped, or are silently skipped — per Constitution Principle VII ("No Silent Writes
/// in Production") and FR-020. Runs before any write reaches
/// <c>IDataDictionaryStore.ApplyAsync</c>.
/// </summary>
public sealed class BreakingChangePolicyEngine
{
    private readonly Action<string> _warningLogger;

    /// <summary>
    /// Creates the policy engine, logging <see cref="OnBreakingChange.Warn"/> messages
    /// to <see cref="Console.Error"/>.
    /// </summary>
    public BreakingChangePolicyEngine()
        : this(warningLogger: null)
    {
    }

    /// <summary>
    /// Creates the policy engine with an injectable warning sink. The project has no
    /// <c>ILogger</c> dependency configured (<c>Core</c> stays free of it, like every
    /// other cross-cutting dependency it does not itself need); a plain
    /// <see cref="Action{T}"/> keeps this simple while still letting a consumer route
    /// <see cref="OnBreakingChange.Warn"/> messages into its own logging pipeline.
    /// </summary>
    /// <param name="warningLogger">
    /// Invoked once per breaking change under <see cref="OnBreakingChange.Warn"/>.
    /// Defaults to writing to <see cref="Console.Error"/> when <see langword="null"/>.
    /// </param>
    public BreakingChangePolicyEngine(Action<string>? warningLogger)
    {
        _warningLogger = warningLogger ?? (message => Console.Error.WriteLine(message));
    }

    /// <summary>
    /// Applies <paramref name="policy"/> to <paramref name="outcome"/>'s
    /// <see cref="SynchronizationOutcome.BreakingChanges"/>.
    /// </summary>
    /// <param name="outcome">The outcome to apply the policy to.</param>
    /// <param name="policy">The configured breaking-change policy.</param>
    /// <returns>
    /// <paramref name="outcome"/> unchanged when there are no breaking changes, or when
    /// <paramref name="policy"/> is <see cref="OnBreakingChange.Warn"/> or
    /// <see cref="OnBreakingChange.Ignore"/> — with <c>BreakingChanges</c> cleared, so
    /// the rest of the outcome (inserts/updates/deactivations) can still be applied.
    /// </returns>
    /// <exception cref="DataDictionarySyncException">
    /// <paramref name="policy"/> is <see cref="OnBreakingChange.Fail"/> (the default)
    /// and <paramref name="outcome"/> carries at least one breaking change. Thrown
    /// before any write is attempted.
    /// </exception>
    public SynchronizationOutcome Apply(SynchronizationOutcome outcome, OnBreakingChange policy)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.BreakingChanges.Count == 0)
        {
            return outcome;
        }

        switch (policy)
        {
            case OnBreakingChange.Fail:
                throw DataDictionarySyncException.ForBreakingChanges(outcome.BreakingChanges);

            case OnBreakingChange.Warn:
                foreach (var breakingChange in outcome.BreakingChanges)
                {
                    _warningLogger(DataDictionarySyncException.DescribeBreakingChange(breakingChange));
                }

                return outcome with { BreakingChanges = [] };

            case OnBreakingChange.Ignore:
                return outcome with { BreakingChanges = [] };

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(policy),
                    policy,
                    "Unrecognized OnBreakingChange policy.");
        }
    }
}
