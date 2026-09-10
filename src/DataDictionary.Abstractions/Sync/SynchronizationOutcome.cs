using DataDictionary.Abstractions.Persistence;

namespace DataDictionary.Abstractions.Sync;

/// <summary>
/// The result of the diff engine comparing a <c>DataDictionaryManifest</c> against
/// the current store state (<see cref="CurrentDictionaryState"/>) for one enum. Fully
/// resolved — never containing an unhandled breaking change — before it reaches
/// <c>IDataDictionaryStore.ApplyAsync</c>; breaking changes are decided by the
/// policy engine first. See <c>data-model.md</c>'s <c>SynchronizationOutcome</c>
/// section.
/// </summary>
/// <param name="ToInsert">Members present in the manifest, absent from the store.</param>
/// <param name="ToUpdate">
/// Members present in both, with a changed compared field.
/// </param>
/// <param name="ToDeactivate">
/// Stored, active rows whose <c>field_name</c> is absent from the manifest, with
/// the code confirmed not in use.
/// </param>
/// <param name="BreakingChanges">
/// Divergences never auto-applied — routed through the <c>OnBreakingChange</c>
/// policy instead of being included in <paramref name="ToInsert"/>,
/// <paramref name="ToUpdate"/>, or <paramref name="ToDeactivate"/>.
/// </param>
/// <param name="Unchanged">
/// Members present in both with no compared field differing. Recorded for
/// observability/logging only; never written.
/// </param>
public sealed record SynchronizationOutcome(
    IReadOnlyList<DictionaryEntry> ToInsert,
    IReadOnlyList<DictionaryEntry> ToUpdate,
    IReadOnlyList<DictionaryEntry> ToDeactivate,
    IReadOnlyList<BreakingChange> BreakingChanges,
    IReadOnlyList<DictionaryEntry> Unchanged);
