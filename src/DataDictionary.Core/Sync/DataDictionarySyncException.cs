using System.Text;
using DataDictionary.Abstractions.Sync;

namespace DataDictionary.Core.Sync;

/// <summary>
/// Thrown by <see cref="BreakingChangePolicyEngine"/> when the startup synchronization
/// pass finds one or more <see cref="BreakingChange"/> entries under the
/// <c>OnBreakingChange.Fail</c> policy (the default). Per Constitution Principle VII
/// ("No Silent Writes in Production") and FR-018/FR-019, the message names — for every
/// affected member — the enum, the field name, the code, and either the referencing
/// table(s) (<see cref="BreakingChangeReason.InUseCodeRemoved"/>) or the colliding
/// field_name (<see cref="BreakingChangeReason.CodeCollision"/>), plus the next step
/// to unblock startup.
/// </summary>
public sealed class DataDictionarySyncException : Exception
{
    /// <summary>Every breaking change that caused this exception.</summary>
    public IReadOnlyList<BreakingChange> BreakingChanges { get; }

    /// <summary>
    /// Creates the exception from one or more breaking changes, building an actionable
    /// message naming every one of them.
    /// </summary>
    /// <param name="breakingChanges">
    /// The breaking changes to report. Must contain at least one entry.
    /// </param>
    public DataDictionarySyncException(IReadOnlyList<BreakingChange> breakingChanges)
        : base(BuildMessage(breakingChanges))
    {
        BreakingChanges = breakingChanges;
    }

    /// <summary>
    /// Builds a <see cref="DataDictionarySyncException"/> for a non-empty list of
    /// breaking changes, throwing <see cref="ArgumentException"/> instead if the list
    /// is empty (an exception naming zero breaking changes would not be actionable).
    /// </summary>
    /// <param name="breakingChanges">The breaking changes to report.</param>
    internal static DataDictionarySyncException ForBreakingChanges(IReadOnlyList<BreakingChange> breakingChanges)
    {
        ArgumentNullException.ThrowIfNull(breakingChanges);

        if (breakingChanges.Count == 0)
        {
            throw new ArgumentException(
                "At least one BreakingChange is required to build a DataDictionarySyncException.",
                nameof(breakingChanges));
        }

        return new DataDictionarySyncException(breakingChanges);
    }

    /// <summary>
    /// Renders one breaking change into a single actionable line, naming the enum,
    /// field name, code, and (per <see cref="BreakingChange.Reason"/>) either the
    /// referencing table(s) or the colliding field_name, plus the next step.
    /// </summary>
    /// <param name="breakingChange">The breaking change to describe.</param>
    internal static string DescribeBreakingChange(BreakingChange breakingChange)
    {
        ArgumentNullException.ThrowIfNull(breakingChange);

        return breakingChange.Reason switch
        {
            BreakingChangeReason.InUseCodeRemoved =>
                $"Enum '{breakingChange.EnumKey}': member '{breakingChange.FieldName}' " +
                $"(code '{breakingChange.Code}') was removed from the manifest but its code is " +
                $"still referenced by {DescribeList(breakingChange.ReferencingTables, "at least one table")}. " +
                "Next step: restore the member in the enum, or retire the referencing business " +
                "data before removing it.",
            BreakingChangeReason.CodeCollision =>
                $"Enum '{breakingChange.EnumKey}': member '{breakingChange.FieldName}' resolves " +
                $"to code '{breakingChange.Code}', which collides with the already-stored, active " +
                $"code of field '{DescribeList(breakingChange.ReferencingTables, "another field")}'. " +
                "Next step: choose a non-colliding code for one of the two members.",
            _ =>
                $"Enum '{breakingChange.EnumKey}': member '{breakingChange.FieldName}' " +
                $"(code '{breakingChange.Code}') has an unrecognized breaking-change reason " +
                $"'{breakingChange.Reason}'. Next step: report this as a library defect.",
        };
    }

    private static string DescribeList(IReadOnlyList<string> values, string fallback)
        => values.Count == 0 ? fallback : string.Join(", ", values);

    private static string BuildMessage(IReadOnlyList<BreakingChange> breakingChanges)
    {
        ArgumentNullException.ThrowIfNull(breakingChanges);

        var builder = new StringBuilder();
        builder.Append("Data dictionary synchronization aborted: ");
        builder.Append(breakingChanges.Count);
        builder.Append(breakingChanges.Count == 1
            ? " breaking change was detected."
            : " breaking changes were detected.");

        foreach (var breakingChange in breakingChanges)
        {
            builder.AppendLine();
            builder.Append(" - ");
            builder.Append(DescribeBreakingChange(breakingChange));
        }

        return builder.ToString();
    }
}
