namespace DataDictionary.Generator.Internal;

/// <summary>
/// Minimal, allocation-free hash-code combiner mirroring
/// <c>DataDictionary.Abstractions.Internal.HashCode</c>. The generator project cannot
/// reference <c>DataDictionary.Abstractions</c> (it only ever emits fully-qualified
/// source text against it — see the project's own file header comment), so every
/// internal pipeline model that needs a hand-rolled, structural
/// <c>Equals</c>/<c>GetHashCode</c> pair (per <c>research.md</c> §3) rolls its own copy
/// of this tiny helper instead of sharing the Abstractions one.
/// </summary>
internal static class HashCode
{
    /// <summary>The starting accumulator for a new hash-combine chain.</summary>
    internal const int Seed = 17;

    /// <summary>Folds <paramref name="value"/>'s hash code into <paramref name="hash"/>.</summary>
    internal static int Combine<T>(int hash, T value)
    {
        unchecked
        {
            return (hash * 31) + (value is null ? 0 : value.GetHashCode());
        }
    }
}
