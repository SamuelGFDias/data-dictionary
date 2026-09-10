namespace DataDictionary.Abstractions.Sync;

/// <summary>
/// The result of <c>IDataDictionaryStore.IsCodeInUseAsync</c>: whether a given
/// <c>(enumKey, code)</c> pair is currently referenced by any business data.
/// </summary>
/// <param name="InUse">
/// Whether the code is currently referenced by at least one business entity.
/// </param>
/// <param name="ReferencingTables">
/// The identity of every table referencing the code. Empty when
/// <paramref name="InUse"/> is <see langword="false"/>.
/// </param>
public sealed record CodeUsageResult(bool InUse, IReadOnlyList<string> ReferencingTables);
