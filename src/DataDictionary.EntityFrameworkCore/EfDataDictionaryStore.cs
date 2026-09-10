using DataDictionary.Abstractions;
using DataDictionary.Abstractions.Persistence;
using DataDictionary.Abstractions.Sync;
using Microsoft.EntityFrameworkCore;

namespace DataDictionary.EntityFrameworkCore;

/// <summary>
/// The EF Core implementation of <see cref="IDataDictionaryStore"/> — the seam
/// through which <c>DataDictionary.Core</c>'s diff/orchestration engine reads and
/// writes the dictionary tables mapped by
/// <see cref="ModelBuilderExtensions.ApplyDataDictionary"/> (T038). See
/// <c>contracts/store-contract.md</c>.
/// </summary>
/// <remarks>
/// This phase (User Story 1, T044/T045) implements only <see cref="GetCurrentAsync"/>
/// and the insert-only path of <see cref="ApplyAsync"/> — enough for a first sync
/// against an empty database (<c>quickstart.md</c> Scenario A). <see cref="ApplyAsync"/>
/// throws <see cref="NotSupportedException"/> if handed an outcome carrying updates or
/// deactivations, since that classification does not exist in the diff engine yet
/// either. <see cref="IsCodeInUseAsync"/> and <see cref="AcquireLockAsync"/> are out of
/// scope for this phase entirely (later user stories — breaking-change detection and
/// concurrent-replica-boot locking, respectively) and throw
/// <see cref="NotImplementedException"/> until then.
/// </remarks>
/// <param name="dbContext">
/// The consumer's own <see cref="DbContext"/> — the same instance whose
/// <c>OnModelCreating</c> called <see cref="ModelBuilderExtensions.ApplyDataDictionary"/>,
/// so <see cref="DictionaryEntry"/> and <see cref="DictionaryEnumCatalogEntry"/> are
/// already part of its model.
/// </param>
public sealed class EfDataDictionaryStore(DbContext dbContext) : IDataDictionaryStore
{
    private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc/>
    public async Task<CurrentDictionaryState> GetCurrentAsync(
        string enumKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(enumKey);

        var entries = await _dbContext.Set<DictionaryEntry>()
            .Where(e => e.EnumKey == enumKey)
            .ToListAsync(cancellationToken);

        var catalogEntry = await _dbContext.Set<DictionaryEnumCatalogEntry>()
            .SingleOrDefaultAsync(e => e.EnumKey == enumKey, cancellationToken);

        return new CurrentDictionaryState(enumKey, entries, catalogEntry);
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">
    /// <paramref name="outcome"/> carries any <see cref="SynchronizationOutcome.ToUpdate"/>
    /// or <see cref="SynchronizationOutcome.ToDeactivate"/> entries — not supported until
    /// a later user story implements that classification and its persistence.
    /// </exception>
    public async Task ApplyAsync(
        SynchronizationOutcome outcome,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.ToUpdate.Count > 0 || outcome.ToDeactivate.Count > 0)
        {
            throw new NotSupportedException(
                "EfDataDictionaryStore.ApplyAsync currently supports insert-only " +
                "outcomes (User Story 1 scope). Update and deactivate support land in " +
                "later user stories.");
        }

        if (outcome.ToInsert.Count == 0)
        {
            return;
        }

        // A single SaveChangesAsync call commits every ToInsert row for this enum in
        // one database transaction, satisfying the "apply the whole outcome
        // atomically per enum" contract.
        await _dbContext.Set<DictionaryEntry>().AddRangeAsync(outcome.ToInsert, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">
    /// Always — breaking-change detection (inspecting the consumer's own data model
    /// for in-use codes) is implemented in a later user story.
    /// </exception>
    public Task<CodeUsageResult> IsCodeInUseAsync(
        string enumKey,
        string code,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "IsCodeInUseAsync is out of scope for User Story 1 (T044/T045) and is " +
            "implemented in the user story covering breaking-change detection.");

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">
    /// Always — the distributed advisory lock used for concurrent-replica boot is
    /// implemented in a later user story.
    /// </exception>
    public Task<LockAcquisitionResult> AcquireLockAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "AcquireLockAsync is out of scope for User Story 1 (T044/T045) and is " +
            "implemented in the user story covering concurrent replica boot.");
}
