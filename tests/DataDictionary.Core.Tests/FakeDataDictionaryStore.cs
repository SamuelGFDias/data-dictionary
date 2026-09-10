using DataDictionary.Abstractions;
using DataDictionary.Abstractions.Sync;

namespace DataDictionary.Core.Tests;

/// <summary>
/// A minimal, hand-rolled fake of <see cref="IDataDictionaryStore"/> for Core unit
/// tests. This test project has no mocking library (Moq/NSubstitute); this fake
/// implements <see cref="IsCodeInUseAsync"/> (controllable via the constructor's
/// <c>(enumKey, code) -&gt; CodeUsageResult</c> lookup) and, optionally,
/// <see cref="GetCurrentAsync"/> (controllable via the constructor's
/// <c>enumKey -&gt; CurrentDictionaryState</c> lookup), plus records every
/// <see cref="ApplyAsync"/> call via <see cref="ApplyAsyncCalled"/> and
/// <see cref="ApplyAsyncCallCount"/> so a synchronizer-level test can assert the write
/// path was never reached. <see cref="AcquireLockAsync"/> still throws
/// <see cref="NotImplementedException"/> — no test using this fake exercises it.
/// </summary>
internal sealed class FakeDataDictionaryStore : IDataDictionaryStore
{
    private readonly IReadOnlyDictionary<(string EnumKey, string Code), CodeUsageResult> _codeUsageResults;
    private readonly IReadOnlyDictionary<string, CurrentDictionaryState> _currentStates;

    /// <param name="codeUsageResults">
    /// The result <see cref="IsCodeInUseAsync"/> returns for each configured
    /// <c>(enumKey, code)</c> pair. Calling with a pair not present in this dictionary
    /// throws <see cref="InvalidOperationException"/> instead of guessing a default —
    /// a test asserting a code path should not call this method must configure no
    /// entries at all, and any unexpected call fails loudly rather than silently
    /// returning "not in use".
    /// </param>
    /// <param name="currentStates">
    /// The result <see cref="GetCurrentAsync"/> returns for each configured
    /// <c>enumKey</c>. Defaults to empty. Calling with an <c>enumKey</c> not present
    /// throws <see cref="InvalidOperationException"/>, for the same "fail loudly on an
    /// unexpected call" reason as <paramref name="codeUsageResults"/>.
    /// </param>
    public FakeDataDictionaryStore(
        IReadOnlyDictionary<(string EnumKey, string Code), CodeUsageResult> codeUsageResults,
        IReadOnlyDictionary<string, CurrentDictionaryState>? currentStates = null)
    {
        _codeUsageResults = codeUsageResults ?? throw new ArgumentNullException(nameof(codeUsageResults));
        _currentStates = currentStates ?? new Dictionary<string, CurrentDictionaryState>();
    }

    /// <summary>Whether <see cref="ApplyAsync"/> has been called at least once.</summary>
    public bool ApplyAsyncCalled => ApplyAsyncCallCount > 0;

    /// <summary>How many times <see cref="ApplyAsync"/> has been called.</summary>
    public int ApplyAsyncCallCount { get; private set; }

    /// <inheritdoc/>
    public Task<CodeUsageResult> IsCodeInUseAsync(
        string enumKey,
        string code,
        CancellationToken cancellationToken)
    {
        if (_codeUsageResults.TryGetValue((enumKey, code), out var result))
        {
            return Task.FromResult(result);
        }

        throw new InvalidOperationException(
            $"FakeDataDictionaryStore.IsCodeInUseAsync was called for enumKey='{enumKey}', " +
            $"code='{code}', but no result was configured for this pair.");
    }

    /// <inheritdoc/>
    public Task<CurrentDictionaryState> GetCurrentAsync(string enumKey, CancellationToken cancellationToken)
    {
        if (_currentStates.TryGetValue(enumKey, out var state))
        {
            return Task.FromResult(state);
        }

        throw new InvalidOperationException(
            $"FakeDataDictionaryStore.GetCurrentAsync was called for enumKey='{enumKey}', " +
            "but no CurrentDictionaryState was configured for it.");
    }

    /// <inheritdoc/>
    public Task ApplyAsync(SynchronizationOutcome outcome, CancellationToken cancellationToken)
    {
        ApplyAsyncCallCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<LockAcquisitionResult> AcquireLockAsync(TimeSpan timeout, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
