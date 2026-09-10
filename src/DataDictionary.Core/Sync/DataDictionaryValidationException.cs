using System.Text;
using DataDictionary.Abstractions.Sync;

namespace DataDictionary.Core.Sync;

/// <summary>
/// Thrown by <see cref="DataDictionarySynchronizer"/> when
/// <see cref="Abstractions.Configuration.SyncMode.ValidateOnly"/> finds any divergence
/// at all between the manifest and the stored dictionary for one enum — not only
/// breaking changes. Per <c>spec.md</c> User Story 2, Acceptance Scenario 3 ("Given the
/// synchronization mode is <c>ValidateOnly</c> and any divergence exists between the
/// enum and the stored dictionary, When the application starts, Then the boot fails and
/// nothing is written to the database"), a pending insert, update, or deactivation —
/// with no breaking change at all — must fail the boot exactly like a breaking change
/// does. This is a distinct type from <see cref="DataDictionarySyncException"/>, which
/// stays specific to the <c>OnBreakingChange</c> policy outcome for breaking changes; a
/// plain, resolvable divergence under <c>ValidateOnly</c> is not the same failure mode
/// and is never routed through that type.
/// </summary>
public sealed class DataDictionaryValidationException : Exception
{
    /// <summary>The affected enum's stable identifier.</summary>
    public string EnumKey { get; }

    /// <summary>The original, pre-policy outcome that caused this exception.</summary>
    public SynchronizationOutcome Outcome { get; }

    /// <summary>
    /// Creates the exception for one enum's divergent outcome, building an actionable
    /// message naming the enum and how many entries fall into each category.
    /// </summary>
    /// <param name="enumKey">The affected enum's stable identifier.</param>
    /// <param name="outcome">The original, pre-policy outcome that diverges.</param>
    public DataDictionaryValidationException(string enumKey, SynchronizationOutcome outcome)
        : base(BuildMessage(enumKey, outcome))
    {
        EnumKey = enumKey;
        Outcome = outcome;
    }

    /// <summary>
    /// Builds a <see cref="DataDictionaryValidationException"/> for an <paramref
    /// name="outcome"/> that carries at least one divergent entry, throwing
    /// <see cref="ArgumentException"/> instead if it carries none (an exception naming
    /// zero divergences would not be actionable).
    /// </summary>
    /// <param name="enumKey">The affected enum's stable identifier.</param>
    /// <param name="outcome">The original, pre-policy outcome to report.</param>
    internal static DataDictionaryValidationException ForDivergence(string enumKey, SynchronizationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (!HasDivergence(outcome))
        {
            throw new ArgumentException(
                "At least one divergent entry (ToInsert, ToUpdate, ToDeactivate, or " +
                "BreakingChanges) is required to build a DataDictionaryValidationException.",
                nameof(outcome));
        }

        return new DataDictionaryValidationException(enumKey, outcome);
    }

    /// <summary>
    /// Whether <paramref name="outcome"/> carries any divergence at all — an entry in
    /// <see cref="SynchronizationOutcome.ToInsert"/>,
    /// <see cref="SynchronizationOutcome.ToUpdate"/>,
    /// <see cref="SynchronizationOutcome.ToDeactivate"/>, or
    /// <see cref="SynchronizationOutcome.BreakingChanges"/>. <see
    /// cref="SynchronizationOutcome.Unchanged"/> never counts as divergence.
    /// </summary>
    /// <param name="outcome">The outcome to inspect.</param>
    internal static bool HasDivergence(SynchronizationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return outcome.ToInsert.Count > 0
            || outcome.ToUpdate.Count > 0
            || outcome.ToDeactivate.Count > 0
            || outcome.BreakingChanges.Count > 0;
    }

    private static string BuildMessage(string enumKey, SynchronizationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var builder = new StringBuilder();
        builder.Append("Data dictionary synchronization aborted: ValidateOnly mode found ");
        builder.Append("divergence for enum '");
        builder.Append(enumKey);
        builder.Append("' — ");
        builder.Append(outcome.ToInsert.Count);
        builder.Append(" to insert, ");
        builder.Append(outcome.ToUpdate.Count);
        builder.Append(" to update, ");
        builder.Append(outcome.ToDeactivate.Count);
        builder.Append(" to deactivate");

        if (outcome.BreakingChanges.Count > 0)
        {
            builder.Append(", ");
            builder.Append(outcome.BreakingChanges.Count);
            builder.Append(outcome.BreakingChanges.Count == 1
                ? " breaking change"
                : " breaking changes");
        }

        builder.Append(". Nothing was written. Next step: restart in Sync mode to apply, " +
            "or resolve the divergence.");

        return builder.ToString();
    }
}
