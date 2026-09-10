using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Persistence;
using DataDictionary.Abstractions.Sync;

namespace DataDictionary.Core.Sync;

/// <summary>
/// Compares a <see cref="ManifestEnumEntry"/> against the current persisted state for
/// that enum (<see cref="CurrentDictionaryState"/>) and produces a
/// <see cref="SynchronizationOutcome"/> — the seam between the compile-time manifest and
/// <c>IDataDictionaryStore.ApplyAsync</c>. See <c>data-model.md</c>'s
/// <c>SynchronizationOutcome</c> section and <c>contracts/store-contract.md</c>.
/// </summary>
/// <remarks>
/// Insert-only for now (User Story 1 / T043): every manifest member whose
/// <c>field_name</c> is absent from <paramref name="currentState"/> lands in
/// <c>ToInsert</c>. Update, deactivate, and breaking-change classification are added by
/// later user stories (T052, T053, T059) without changing this class's public surface.
/// </remarks>
public sealed class DictionaryDiffEngine
{
    /// <summary>
    /// Diffs <paramref name="manifestEnum"/> against <paramref name="currentState"/> and
    /// returns the resulting <see cref="SynchronizationOutcome"/> for this enum.
    /// </summary>
    /// <param name="manifestEnum">The manifest's entry for this enum.</param>
    /// <param name="currentState">
    /// The current persisted state for the same enum, as returned by
    /// <c>IDataDictionaryStore.GetCurrentAsync</c>.
    /// </param>
    public SynchronizationOutcome Diff(
        ManifestEnumEntry manifestEnum,
        CurrentDictionaryState currentState)
    {
        ArgumentNullException.ThrowIfNull(manifestEnum);
        ArgumentNullException.ThrowIfNull(currentState);

        var existingFieldNames = new HashSet<string>(
            currentState.Entries.Select(entry => entry.FieldName),
            StringComparer.Ordinal);

        var toInsert = new List<DictionaryEntry>();

        foreach (var member in manifestEnum.Members)
        {
            if (existingFieldNames.Contains(member.FieldName))
            {
                continue;
            }

            toInsert.Add(new DictionaryEntry
            {
                EnumKey = manifestEnum.EnumKey,
                FieldName = member.FieldName,
                Code = member.Code,
                NumericValue = member.NumericValue,
                Description = member.Description,
                GroupName = member.GroupName,
                IsDeprecated = member.IsDeprecated,
                SortOrder = member.SortOrder,
                IsActive = true,
            });
        }

        return new SynchronizationOutcome(
            ToInsert: toInsert,
            ToUpdate: [],
            ToDeactivate: [],
            BreakingChanges: [],
            Unchanged: []);
    }
}
