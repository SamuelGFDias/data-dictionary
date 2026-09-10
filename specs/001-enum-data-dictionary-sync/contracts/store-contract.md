# Contract: `IDataDictionaryStore` (`DataDictionary.Core`)

This is the single seam between `Core` and any persistence technology. `Core` depends
only on this interface (and the compile-time manifest types); it never references EF
Core, Dapper, or any other ORM package, directly or transitively — this is Constitution
Principle III, and this contract is the mechanism that makes it possible: every provider
package (starting with `DataDictionary.EntityFrameworkCore`) implements this interface
and owns its own ORM dependency, while `Core`'s diff/fail-fast/lock-orchestration engine
is written once against the interface and is reusable by any future provider (a Dapper
provider, for instance) without modification.

```csharp
namespace DataDictionary.Abstractions;

public interface IDataDictionaryStore
{
    /// <summary>
    /// Reads the current persisted state of the dictionary for one enum key —
    /// every active and inactive DictionaryEntry row for that enum_key, plus the
    /// catalog entry if the optional catalog table is enabled.
    /// </summary>
    Task<CurrentDictionaryState> GetCurrentAsync(
        string enumKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a SynchronizationOutcome that has already been fully resolved
    /// (inserts, updates, deactivations only — never a breaking change; breaking
    /// changes are decided by Core's policy engine before this is ever called).
    /// Implementations MUST apply the whole outcome atomically per enum.
    /// </summary>
    Task ApplyAsync(
        SynchronizationOutcome outcome,
        CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether a given (enumKey, code) pair is currently referenced by
    /// any business data, by inspecting the consuming application's own data model
    /// (see research.md §7) — not by consulting DictionaryEntry bookkeeping alone.
    /// Returns the identity of at least one referencing table when true, so Core
    /// can build the FR-018 error message without a second round-trip.
    /// </summary>
    Task<CodeUsageResult> IsCodeInUseAsync(
        string enumKey,
        string code,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to acquire the distributed synchronization lock for the whole
    /// sync pass (not per-enum). Returns a disposable lock handle on success, or
    /// a result indicating the lock is held elsewhere so the caller can fall back
    /// to ValidateOnly behavior (FR-024). Implementations use each database's
    /// native advisory-lock primitive (research.md §6); no external dependency.
    /// </summary>
    Task<LockAcquisitionResult> AcquireLockAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
```

## Why `Core` never references an ORM

Every method above is expressed purely in terms of `Abstractions`-owned types
(`CurrentDictionaryState`, `SynchronizationOutcome`, `CodeUsageResult`,
`LockAcquisitionResult` — all POCOs/records with no ORM types in their signatures). The
diff engine in `Core` that decides *what* to insert/update/deactivate, and the policy
engine that decides *whether* a breaking change aborts boot, operate entirely on these
types and on the compile-time manifest. Nothing in `Core` needs to know whether the
implementation behind `IDataDictionaryStore` talks to EF Core, Dapper, or a hand-rolled
`DbConnection` — that decision is fully encapsulated in the provider package.

## Supporting types (owned by `DataDictionary.Abstractions`)

- `CurrentDictionaryState` — the current `DictionaryEntry` rows (see `data-model.md`) for
  one `enum_key`, plus the catalog entry if present.
- `CodeUsageResult` — `bool InUse` plus `IReadOnlyList<string> ReferencingTables` (empty
  when `InUse` is `false`).
- `LockAcquisitionResult` — either an `IAsyncDisposable` lock handle, or a value
  indicating the lock was not obtained (never an exception for the "someone else holds
  it" case — that is an expected, handled outcome per FR-024, not a fault).

## Compatibility note

This interface is part of the library's public contract (Constitution Principle I:
breaking changes to it require a MAJOR version). Its exact parameter/return shapes are
finalized during implementation against this contract's intent; any shape change that
alters behavior visible to a provider author is a breaking change under Principle I.
