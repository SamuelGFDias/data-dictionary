using DataDictionary.Abstractions;
using DataDictionary.Abstractions.Configuration;

namespace DataDictionary.Core.Sync;

/// <summary>
/// The startup synchronization orchestrator: for every registered manifest and every
/// enum in it, reads the current persisted state via <see cref="IDataDictionaryStore.GetCurrentAsync"/>,
/// diffs it against the manifest via <see cref="DictionaryDiffEngine.Diff"/>, and — only
/// when <see cref="DataDictionaryOptions.SyncMode"/> is <see cref="SyncMode.Sync"/> —
/// applies the resulting outcome via <see cref="IDataDictionaryStore.ApplyAsync"/>.
/// </summary>
/// <remarks>
/// Scoped to the <see cref="SyncMode.Sync"/> path only (User Story 1 / T046):
/// <see cref="SyncMode.Off"/> is a no-op, and <see cref="SyncMode.ValidateOnly"/> /
/// <see cref="SyncMode.SyncAndValidate"/> are not implemented yet — they require the
/// breaking-change policy engine and the fail-fast/lock orchestration added by later
/// user stories, and are intentionally left unhandled here rather than approximated.
/// </remarks>
/// <param name="options">The resolved <see cref="DataDictionaryOptions"/>, carrying
/// every registered manifest and the configured <see cref="SyncMode"/>.</param>
/// <param name="store">The persistence seam for the current provider.</param>
/// <param name="diffEngine">Computes the <c>SynchronizationOutcome</c> for one enum.</param>
public sealed class DataDictionarySynchronizer(
    DataDictionaryOptions options,
    IDataDictionaryStore store,
    DictionaryDiffEngine diffEngine)
{
    private readonly DataDictionaryOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IDataDictionaryStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly DictionaryDiffEngine _diffEngine = diffEngine ?? throw new ArgumentNullException(nameof(diffEngine));

    /// <summary>
    /// Runs the configured synchronization pass over every registered manifest.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        if (_options.SyncMode == SyncMode.Off)
        {
            return;
        }

        if (_options.SyncMode != SyncMode.Sync)
        {
            // ValidateOnly / SyncAndValidate are out of scope for User Story 1 (T046)
            // — they land alongside the breaking-change policy engine in a later
            // user story.
            throw new NotSupportedException(
                $"DataDictionarySynchronizer currently supports only {nameof(SyncMode.Sync)} " +
                $"(User Story 1 scope). {_options.SyncMode} is implemented in a later user story.");
        }

        foreach (var manifest in _options.Manifests)
        {
            foreach (var manifestEnum in manifest.Enums)
            {
                var currentState = await _store.GetCurrentAsync(manifestEnum.EnumKey, cancellationToken);
                var outcome = _diffEngine.Diff(manifestEnum, currentState);
                await _store.ApplyAsync(outcome, cancellationToken);
            }
        }
    }
}
