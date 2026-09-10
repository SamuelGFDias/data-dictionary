using DataDictionary.Abstractions;
using DataDictionary.Abstractions.Configuration;

namespace DataDictionary.Core.Sync;

/// <summary>
/// The startup synchronization orchestrator: for every registered manifest and every
/// enum in it, reads the current persisted state via <see cref="IDataDictionaryStore.GetCurrentAsync"/>,
/// diffs it against the manifest via <see cref="DictionaryDiffEngine.DiffAsync"/>,
/// applies the configured <see cref="OnBreakingChange"/> policy via
/// <see cref="BreakingChangePolicyEngine.Apply"/>, and — only when
/// <see cref="DataDictionaryOptions.SyncMode"/> is <see cref="SyncMode.Sync"/> —
/// applies the resolved outcome via <see cref="IDataDictionaryStore.ApplyAsync"/>.
/// </summary>
/// <remarks>
/// Covers <see cref="SyncMode.Sync"/> (User Story 1 / T046) and
/// <see cref="SyncMode.ValidateOnly"/> (User Story 2 / T056), which diverge right after
/// the outcome is computed: <see cref="SyncMode.Sync"/> applies the breaking-change
/// policy (which may abort boot under the default <see cref="OnBreakingChange.Fail"/>)
/// and then calls <see cref="IDataDictionaryStore.ApplyAsync"/>, while
/// <see cref="SyncMode.ValidateOnly"/> instead fails the boot with
/// <see cref="DataDictionaryValidationException"/> whenever the original, pre-policy
/// outcome carries ANY divergence — not only breaking changes — per
/// <c>spec.md</c> User Story 2, Acceptance Scenario 3, and never calls
/// <see cref="IDataDictionaryStore.ApplyAsync"/> at all. <see
/// cref="SyncMode.SyncAndValidate"/> is not implemented yet — it requires the
/// fail-fast/lock orchestration added by a later user story, and is intentionally left
/// unhandled here rather than approximated. <see cref="SyncMode.Off"/> is a no-op.
/// </remarks>
/// <param name="options">The resolved <see cref="DataDictionaryOptions"/>, carrying
/// every registered manifest, the configured <see cref="SyncMode"/>, and the configured
/// <see cref="OnBreakingChange"/> policy.</param>
/// <param name="store">The persistence seam for the current provider.</param>
/// <param name="diffEngine">Computes the <c>SynchronizationOutcome</c> for one enum.</param>
/// <param name="policyEngine">
/// Applies the configured <see cref="OnBreakingChange"/> policy to each enum's outcome
/// before any write is attempted.
/// </param>
public sealed class DataDictionarySynchronizer(
    DataDictionaryOptions options,
    IDataDictionaryStore store,
    DictionaryDiffEngine diffEngine,
    BreakingChangePolicyEngine policyEngine)
{
    private readonly DataDictionaryOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IDataDictionaryStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly DictionaryDiffEngine _diffEngine = diffEngine ?? throw new ArgumentNullException(nameof(diffEngine));
    private readonly BreakingChangePolicyEngine _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));

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

        if (_options.SyncMode != SyncMode.Sync && _options.SyncMode != SyncMode.ValidateOnly)
        {
            // SyncAndValidate remains out of scope (User Story 2 / T056) — it needs
            // the fail-fast/lock orchestration added by a later user story, and is
            // intentionally left unhandled here rather than approximated.
            throw new NotSupportedException(
                $"DataDictionarySynchronizer currently supports only {nameof(SyncMode.Sync)} and " +
                $"{nameof(SyncMode.ValidateOnly)} (User Story 1 & 2 scope). {_options.SyncMode} is " +
                "implemented in a later user story.");
        }

        foreach (var manifest in _options.Manifests)
        {
            foreach (var manifestEnum in manifest.Enums)
            {
                var currentState = await _store.GetCurrentAsync(manifestEnum.EnumKey, cancellationToken)
                    .ConfigureAwait(false);

                var outcome = await _diffEngine.DiffAsync(manifestEnum, currentState, _store, cancellationToken)
                    .ConfigureAwait(false);

                if (_options.SyncMode == SyncMode.ValidateOnly)
                {
                    // Per spec.md User Story 2, Acceptance Scenario 3: ValidateOnly
                    // fails the boot on ANY divergence — not only breaking changes —
                    // checked against the original, pre-policy outcome, before the
                    // breaking-change policy ever runs. This also covers a
                    // breaking-changes-only outcome under the default Fail policy, so
                    // BreakingChangePolicyEngine.Apply's own Fail path never needs to
                    // run for ValidateOnly. Never writes, regardless of the outcome.
                    if (DataDictionaryValidationException.HasDivergence(outcome))
                    {
                        throw DataDictionaryValidationException.ForDivergence(manifestEnum.EnumKey, outcome);
                    }

                    continue;
                }

                // May throw DataDictionarySyncException under the default Fail policy
                // — before any write is attempted.
                var resolvedOutcome = _policyEngine.Apply(outcome, _options.OnBreakingChange);

                await _store.ApplyAsync(resolvedOutcome, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
