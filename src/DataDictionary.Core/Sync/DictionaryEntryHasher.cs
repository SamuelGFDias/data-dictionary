using System.Security.Cryptography;
using System.Text;

namespace DataDictionary.Core.Sync;

/// <summary>
/// Computes the <c>content_hash</c> persisted on <c>DictionaryEntry.ContentHash</c> --
/// a deterministic digest over the fields compared for "did this entry change"
/// (description, group, sort order, deprecated flag), per <c>spec.md</c>'s
/// Assumptions section and <c>data-model.md</c>'s <c>content_hash</c> note.
/// </summary>
/// <remarks>
/// Uses SHA-256 over a stable string representation of the compared fields, not
/// <see cref="object.GetHashCode"/> or <see cref="HashCode.Combine{T1}(T1)"/> -- both of
/// those are randomized per process in .NET (to resist hash-flooding attacks), so a
/// hash computed by one process run could never be compared against a hash persisted
/// by an earlier run, which is exactly the comparison this class exists to support.
/// </remarks>
public static class DictionaryEntryHasher
{
    /// <summary>
    /// The Unicode "unit separator" control character (U+001F), used both as the
    /// placeholder for a <see langword="null"/> string field and as the separator
    /// between fields in the hashed representation. It is vanishingly unlikely to
    /// appear inside a real description or group name, so using it both ways keeps
    /// <c>(null, "x")</c> distinct from <c>("", "x")</c> and keeps two different
    /// field combinations (e.g. description "ab" + group "c" vs. description "a" +
    /// group "bc") from ever joining into the same canonical string.
    /// </summary>
    private static readonly string NullPlaceholder = "NULL";

    private static readonly char FieldSeparator = '';

    /// <summary>
    /// Computes a deterministic, process-restart-stable hex hash over the four fields
    /// compared for change detection: <paramref name="description"/>,
    /// <paramref name="groupName"/>, <paramref name="sortOrder"/> and
    /// <paramref name="isDeprecated"/>.
    /// </summary>
    /// <param name="description">The entry's description, or <see langword="null"/>.</param>
    /// <param name="groupName">The entry's group name, or <see langword="null"/>.</param>
    /// <param name="sortOrder">The entry's sort position.</param>
    /// <param name="isDeprecated">Whether the entry is deprecated.</param>
    /// <returns>A lowercase hex-encoded SHA-256 digest of the four fields.</returns>
    public static string Compute(string? description, string? groupName, int sortOrder, bool isDeprecated)
    {
        var canonical = string.Join(
            FieldSeparator,
            description ?? NullPlaceholder,
            groupName ?? NullPlaceholder,
            sortOrder.ToString(System.Globalization.CultureInfo.InvariantCulture),
            isDeprecated.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
