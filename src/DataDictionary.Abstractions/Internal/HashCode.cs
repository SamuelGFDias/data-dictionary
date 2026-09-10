namespace DataDictionary.Abstractions.Internal;

/// <summary>
/// Minimal, allocation-free hash-code combiner used by the hand-rolled
/// <c>GetHashCode</c> overrides on the compile-time models (<c>EnumDictionaryModel</c>,
/// <c>DictionaryMemberModel</c>, <c>DataDictionaryManifest</c>, <c>ManifestEnumEntry</c>,
/// <c>ManifestMemberEntry</c>). <c>System.HashCode</c> is not available on
/// <c>netstandard2.0</c>, so this type stands in for it — see
/// <c>research.md</c> §3 on why every collection field on those models must be
/// paired with a hand-rolled, structural <c>Equals</c>/<c>GetHashCode</c> pair rather
/// than default reference equality.
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
