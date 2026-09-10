namespace DataDictionary.Abstractions.Sync;

/// <summary>
/// The result of <c>IDataDictionaryStore.AcquireLockAsync</c>: either the
/// distributed synchronization lock's disposable handle, or an indication that the
/// lock is held elsewhere. The "someone else holds it" case is an expected, handled
/// outcome (FR-024) — never represented as an exception.
/// </summary>
public sealed class LockAcquisitionResult
{
    private LockAcquisitionResult(bool isAcquired, IAsyncDisposable? handle)
    {
        IsAcquired = isAcquired;
        Handle = handle;
    }

    /// <summary>
    /// Whether the lock was acquired. When <see langword="true"/>,
    /// <see cref="Handle"/> is non-<see langword="null"/> and must be disposed to
    /// release the lock; when <see langword="false"/>, <see cref="Handle"/> is
    /// <see langword="null"/> and the caller should fall back to
    /// <c>ValidateOnly</c> behavior (FR-024).
    /// </summary>
    public bool IsAcquired { get; }

    /// <summary>
    /// The lock's disposable handle when <see cref="IsAcquired"/> is
    /// <see langword="true"/>; otherwise <see langword="null"/>.
    /// </summary>
    public IAsyncDisposable? Handle { get; }

    /// <summary>Creates a result representing a successfully acquired lock.</summary>
    /// <param name="handle">The lock's disposable handle.</param>
    public static LockAcquisitionResult Acquired(IAsyncDisposable handle)
    {
        return new LockAcquisitionResult(isAcquired: true, handle ?? throw new ArgumentNullException(nameof(handle)));
    }

    /// <summary>
    /// A result representing a lock that could not be acquired because another
    /// process already holds it.
    /// </summary>
    public static LockAcquisitionResult NotAcquired { get; } = new LockAcquisitionResult(isAcquired: false, handle: null);
}
