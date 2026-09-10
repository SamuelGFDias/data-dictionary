using DataDictionary.Abstractions;
using DataDictionary.Abstractions.Sync;

namespace DataDictionary.Core.Tests;

/// <summary>
/// A minimal, hand-rolled fake of <see cref="IDataDictionaryStore"/> for Core diff-
/// engine unit tests. This test project has no mocking library (Moq/NSubstitute); this
/// fake implements only <see cref="IsCodeInUseAsync"/>, controllable via the
/// constructor's <c>(enumKey, code) -&gt; CodeUsageResult</c> lookup. Every other member
/// throws <see cref="NotImplementedException"/> — the diff-engine tests that use this
/// fake never exercise them.
/// </summary>
internal sealed class FakeDataDictionaryStore : IDataDictionaryStore
{
    private readonly IReadOnlyDictionary<(string EnumKey, string Code), CodeUsageResult> _codeUsageResults;

    /// <param name="codeUsageResults">
    /// The result <see cref="IsCodeInUseAsync"/> returns for each configured
    /// <c>(enumKey, code)</c> pair. Calling with a pair not present in this dictionary
    /// throws <see cref="InvalidOperationException"/> instead of guessing a default —
    /// a test asserting a code path should not call this method must configure no
    /// entries at all, and any unexpected call fails loudly rather than silently
    /// returning "not in use".
    /// </param>
    public FakeDataDictionaryStore(
        IReadOnlyDictionary<(string EnumKey, string Code), CodeUsageResult> codeUsageResults)
    {
        _codeUsageResults = codeUsageResults ?? throw new ArgumentNullException(nameof(codeUsageResults));
    }

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
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task ApplyAsync(SynchronizationOutcome outcome, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<LockAcquisitionResult> AcquireLockAsync(TimeSpan timeout, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
