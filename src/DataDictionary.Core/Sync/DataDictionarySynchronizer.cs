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
/// Covers <see cref="SyncMode.Sync"/> (User Story 1 / T046, plus the multi-replica
/// coordination lock from User Story 5 / T070) and <see cref="SyncMode.ValidateOnly"/>
/// (User Story 2 / T056), which diverge right after the outcome is computed:
/// <see cref="SyncMode.Sync"/> applies the breaking-change policy (which may abort boot
/// under the default <see cref="OnBreakingChange.Fail"/>) and then calls
/// <see cref="IDataDictionaryStore.ApplyAsync"/>, while <see cref="SyncMode.ValidateOnly"/>
/// instead fails the boot with <see cref="DataDictionaryValidationException"/> whenever
/// the original, pre-policy outcome carries ANY divergence — not only breaking changes —
/// per <c>spec.md</c> User Story 2, Acceptance Scenario 3, and never calls
/// <see cref="IDataDictionaryStore.ApplyAsync"/> at all.
/// <para>
/// Per FR-024 (User Story 5 / T070), before <see cref="SyncMode.Sync"/> writes anything
/// it must first obtain the store's coordination lock via
/// <see cref="IDataDictionaryStore.AcquireLockAsync"/>, so that when multiple replicas
/// start at once only one of them synchronizes. A replica that cannot obtain the lock
/// runs the exact same validate-and-throw-on-divergence path as an explicit
/// <see cref="SyncMode.ValidateOnly"/> pass instead — it neither writes nor fails boot
/// merely because the lock was unavailable. A replica that does obtain the lock holds it
/// for the whole synchronization pass (every enum of every manifest) and releases it
/// afterwards, including when the pass throws.
/// </para>
/// <see cref="SyncMode.SyncAndValidate"/> is not implemented yet — it requires
/// additional fail-fast orchestration added by a later user story, and is intentionally
/// left unhandled here rather than approximated. <see cref="SyncMode.Off"/> is a no-op.
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

    // Fixed, non-configurable timeout for the FR-024 coordination lock: this is a
    // boot-time handshake between replicas, not a long-running wait, so a replica that
    // cannot get the lock quickly should fall back to ValidateOnly behavior rather than
    // stall startup. No public option is exposed for this on purpose.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs the configured synchronization pass over every registered manifest.
    /// </summary>
    /// <remarks>
    /// For <see cref="SyncMode.Sync"/>, first attempts <see
    /// cref="IDataDictionaryStore.AcquireLockAsync"/> (FR-024 / T070): on success the
    /// lock is held for the entire pass and the usual write behavior runs unchanged; on
    /// failure — another replica already holds it, or <see cref="LockTimeout"/> elapses
    /// first — this call instead runs the exact same validate-and-throw-on-divergence
    /// path as an explicit <see cref="SyncMode.ValidateOnly"/> pass, so a losing replica
    /// neither writes nor fails startup just because it lost the race.
    /// </remarks>
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
            // additional fail-fast orchestration added by a later user story, and is
            // intentionally left unhandled here rather than approximated.
            throw new NotSupportedException(
                $"DataDictionarySynchronizer currently supports only {nameof(SyncMode.Sync)} and " +
                $"{nameof(SyncMode.ValidateOnly)} (User Story 1 & 2 scope). {_options.SyncMode} is " +
                "implemented in a later user story.");
        }

        if (_options.SyncMode == SyncMode.ValidateOnly)
        {
            // ValidateOnly never writes, so the coordination lock — which exists only
            // to serialize writers — is irrelevant here; never call AcquireLockAsync.
            await ValidateWithoutWritingAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // SyncMode.Sync: per FR-024 (T070), a replica must hold the coordination lock
        // for the whole pass before it writes anything. A replica that cannot obtain it
        // — another replica already holds it, or LockTimeout elapses — falls back to
        // the same validate-only semantics as an explicit ValidateOnly pass, rather than
        // blocking startup or writing unsynchronized.
        var lockResult = await _store.AcquireLockAsync(LockTimeout, cancellationToken).ConfigureAwait(false);

        if (!lockResult.IsAcquired)
        {
            await ValidateWithoutWritingAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // The handle is held for the full duration of the pass below and released only
        // once every manifest/enum has been processed — including when the loop throws
        // (e.g. DataDictionarySyncException from the breaking-change policy) — so
        // `await using` guarantees release via DisposeAsync in that case too.
        await using var lockHandle = lockResult.Handle!;

        foreach (var manifest in _options.Manifests)
        {
            foreach (var manifestEnum in manifest.Enums)
            {
                var currentState = await _store.GetCurrentAsync(manifestEnum.EnumKey, cancellationToken)
                    .ConfigureAwait(false);

                var outcome = await _diffEngine.DiffAsync(manifestEnum, currentState, _store, cancellationToken)
                    .ConfigureAwait(false);

                // May throw DataDictionarySyncException under the default Fail policy
                // — before any write is attempted.
                var resolvedOutcome = _policyEngine.Apply(outcome, _options.OnBreakingChange);

                await _store.ApplyAsync(resolvedOutcome, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The shared validate-only pass: used both for an explicit <see
    /// cref="SyncMode.ValidateOnly"/> run and for a <see cref="SyncMode.Sync"/> replica
    /// that could not obtain the coordination lock (FR-024 / T070) — both cases must
    /// produce identical observable behavior for the same manifests. Per spec.md User
    /// Story 2, Acceptance Scenario 3: fails the boot on ANY divergence — not only
    /// breaking changes — checked against the original, pre-policy outcome, before the
    /// breaking-change policy ever runs. This also covers a breaking-changes-only
    /// outcome under the default Fail policy, so <see cref="BreakingChangePolicyEngine.Apply"/>'s
    /// own Fail path never needs to run here. Never calls <see
    /// cref="IDataDictionaryStore.ApplyAsync"/>, regardless of the outcome.
    /// </summary>
    private async Task ValidateWithoutWritingAsync(CancellationToken cancellationToken)
    {
        foreach (var manifest in _options.Manifests)
        {
            foreach (var manifestEnum in manifest.Enums)
            {
                var currentState = await _store.GetCurrentAsync(manifestEnum.EnumKey, cancellationToken)
                    .ConfigureAwait(false);

                var outcome = await _diffEngine.DiffAsync(manifestEnum, currentState, _store, cancellationToken)
                    .ConfigureAwait(false);

                if (DataDictionaryValidationException.HasDivergence(outcome))
                {
                    throw DataDictionaryValidationException.ForDivergence(manifestEnum.EnumKey, outcome);
                }
            }
        }
    }
}
