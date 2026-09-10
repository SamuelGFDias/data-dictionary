using System.Threading;
using DataDictionary.Abstractions.Sync;

namespace DataDictionary.Abstractions;

/// <summary>
/// The single seam between <c>DataDictionary.Core</c> and any persistence
/// technology. <c>Core</c> depends only on this interface (and the compile-time
/// manifest types); it never references EF Core, Dapper, or any other ORM package,
/// directly or transitively (Constitution Principle III). Every provider package
/// (starting with <c>DataDictionary.EntityFrameworkCore</c>) implements this
/// interface and owns its own ORM dependency. See
/// <c>contracts/store-contract.md</c>.
/// </summary>
public interface IDataDictionaryStore
{
    /// <summary>
    /// Reads the current persisted state of the dictionary for one enum key —
    /// every active and inactive <c>DictionaryEntry</c> row for that
    /// <paramref name="enumKey"/>, plus the catalog entry if the optional catalog
    /// table is enabled.
    /// </summary>
    /// <param name="enumKey">The enum's stable identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<CurrentDictionaryState> GetCurrentAsync(
        string enumKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a <see cref="SynchronizationOutcome"/> that has already been fully
    /// resolved (inserts, updates, deactivations only — never a breaking change;
    /// breaking changes are decided by <c>Core</c>'s policy engine before this is
    /// ever called). Implementations MUST apply the whole outcome atomically per
    /// enum.
    /// </summary>
    /// <param name="outcome">The fully resolved outcome to apply.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ApplyAsync(
        SynchronizationOutcome outcome,
        CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether a given <paramref name="enumKey"/>/<paramref name="code"/>
    /// pair is currently referenced by any business data, by inspecting the
    /// consuming application's own data model — not by consulting
    /// <c>DictionaryEntry</c> bookkeeping alone. Returns the identity of at least
    /// one referencing table when in use, so <c>Core</c> can build the FR-018 error
    /// message without a second round-trip.
    /// </summary>
    /// <param name="enumKey">The enum's stable identifier.</param>
    /// <param name="code">The dictionary code to check for usage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<CodeUsageResult> IsCodeInUseAsync(
        string enumKey,
        string code,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to acquire the distributed synchronization lock for the whole sync
    /// pass (not per-enum). Returns a disposable lock handle on success, or a
    /// result indicating the lock is held elsewhere so the caller can fall back to
    /// <c>ValidateOnly</c> behavior (FR-024). Implementations use each database's
    /// native advisory-lock primitive; no external dependency.
    /// </summary>
    /// <param name="timeout">How long to wait for the lock before giving up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<LockAcquisitionResult> AcquireLockAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
