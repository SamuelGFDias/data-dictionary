using DataDictionary.Abstractions.Persistence;

namespace DataDictionary.Abstractions.Sync;

/// <summary>
/// The current persisted state of the dictionary for one <c>enum_key</c> — every
/// active and inactive <see cref="DictionaryEntry"/> row for that enum, plus the
/// catalog entry if the optional catalog table is enabled. Returned by
/// <c>IDataDictionaryStore.GetCurrentAsync</c> and diffed against the manifest to
/// produce a <see cref="SynchronizationOutcome"/>.
/// </summary>
/// <param name="EnumKey">The enum's stable identifier.</param>
/// <param name="Entries">
/// Every persisted row for this enum, active and inactive alike.
/// </param>
/// <param name="CatalogEntry">
/// The enum's catalog entry, or <see langword="null"/> when the optional catalog
/// table is disabled or no entry exists yet.
/// </param>
public sealed record CurrentDictionaryState(
    string EnumKey,
    IReadOnlyList<DictionaryEntry> Entries,
    DictionaryEnumCatalogEntry? CatalogEntry);
