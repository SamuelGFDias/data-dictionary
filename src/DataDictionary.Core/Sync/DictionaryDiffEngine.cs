using DataDictionary.Abstractions;
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
/// Classifies inserts (User Story 1 / T043) and the two breaking-change divergences
/// (User Story 2 / T053): a stored, active member whose <c>field_name</c> is absent
/// from the manifest is routed to <see cref="SynchronizationOutcome.BreakingChanges"/>
/// (<see cref="BreakingChangeReason.InUseCodeRemoved"/>) when
/// <c>IDataDictionaryStore.IsCodeInUseAsync</c> reports the code still in use, or to
/// <see cref="SynchronizationOutcome.ToDeactivate"/> otherwise; a manifest member whose
/// resolved <c>code</c> collides with a different, active, stored <c>field_name</c>'s
/// code is routed to <see cref="SynchronizationOutcome.BreakingChanges"/>
/// (<see cref="BreakingChangeReason.CodeCollision"/>) instead of being inserted or
/// updated. Full update/unchanged-field classification for members present in both the
/// manifest and the store is added by User Story 3 (T057–T059) without changing this
/// class's public surface.
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
    /// <param name="store">
    /// The persistence seam, used to resolve <c>IsCodeInUseAsync</c> for every stored,
    /// active member whose <c>field_name</c> is absent from the manifest.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<SynchronizationOutcome> DiffAsync(
        ManifestEnumEntry manifestEnum,
        CurrentDictionaryState currentState,
        IDataDictionaryStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestEnum);
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(store);

        // field_name -> stored row (active or inactive), for insert/match detection.
        var currentByFieldName = new Dictionary<string, DictionaryEntry>(StringComparer.Ordinal);

        // code -> field_name, active rows only — DictionaryEntry.Code is unique per
        // enum_key only among active rows (see DictionaryEntry.Code's doc), so only
        // active rows can collide.
        var activeCodeOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in currentState.Entries)
        {
            currentByFieldName[entry.FieldName] = entry;

            if (entry.IsActive)
            {
                activeCodeOwners[entry.Code] = entry.FieldName;
            }
        }

        var manifestFieldNames = new HashSet<string>(
            manifestEnum.Members.Select(member => member.FieldName),
            StringComparer.Ordinal);

        var toInsert = new List<DictionaryEntry>();
        var toDeactivate = new List<DictionaryEntry>();
        var breakingChanges = new List<BreakingChange>();

        foreach (var member in manifestEnum.Members)
        {
            // FR-019: this member's resolved code already belongs, actively, to a
            // *different* stored field_name — never silently overwrite.
            if (activeCodeOwners.TryGetValue(member.Code, out var owningFieldName)
                && !string.Equals(owningFieldName, member.FieldName, StringComparison.Ordinal))
            {
                breakingChanges.Add(new BreakingChange(
                    EnumKey: manifestEnum.EnumKey,
                    FieldName: member.FieldName,
                    Code: member.Code,
                    Reason: BreakingChangeReason.CodeCollision,
                    // BreakingChange.ReferencingTables is documented as populated only
                    // for InUseCodeRemoved; there is no dedicated field on the shared
                    // Abstractions contract for "the field_name that already owns this
                    // code", so it is reused here to carry that identity through to
                    // DataDictionarySyncException's message. See T053/T055 notes.
                    ReferencingTables: [owningFieldName]));
                continue;
            }

            if (currentByFieldName.ContainsKey(member.FieldName))
            {
                // Present in both the manifest and the store, no collision. Full
                // update-vs-unchanged classification for this case is User Story 3
                // scope (T057–T059) and is intentionally left unhandled here.
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

        foreach (var entry in currentState.Entries)
        {
            if (!entry.IsActive || manifestFieldNames.Contains(entry.FieldName))
            {
                continue;
            }

            // FR-018: a stored, active member's field_name is absent from the
            // manifest — only safe to deactivate when its code is confirmed unused.
            var usage = await store.IsCodeInUseAsync(manifestEnum.EnumKey, entry.Code, cancellationToken)
                .ConfigureAwait(false);

            if (usage.InUse)
            {
                breakingChanges.Add(new BreakingChange(
                    EnumKey: manifestEnum.EnumKey,
                    FieldName: entry.FieldName,
                    Code: entry.Code,
                    Reason: BreakingChangeReason.InUseCodeRemoved,
                    ReferencingTables: usage.ReferencingTables));
                continue;
            }

            // Basic ToDeactivate population (full incremental-sync semantics are User
            // Story 4 scope) — safe here because the code is confirmed unused above.
            toDeactivate.Add(new DictionaryEntry
            {
                EnumKey = entry.EnumKey,
                FieldName = entry.FieldName,
                Code = entry.Code,
                NumericValue = entry.NumericValue,
                Description = entry.Description,
                GroupName = entry.GroupName,
                IsDeprecated = entry.IsDeprecated,
                SortOrder = entry.SortOrder,
                IsActive = false,
                ContentHash = entry.ContentHash,
                CreatedAt = entry.CreatedAt,
                UpdatedAt = entry.UpdatedAt,
            });
        }

        return new SynchronizationOutcome(
            ToInsert: toInsert,
            ToUpdate: [],
            ToDeactivate: toDeactivate,
            BreakingChanges: breakingChanges,
            Unchanged: []);
    }
}
