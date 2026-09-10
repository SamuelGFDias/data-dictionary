using System.Collections.Immutable;
using DataDictionary.Abstractions.Internal;

namespace DataDictionary.Abstractions.Manifest;

/// <summary>
/// Compile-time model of one <see langword="enum"/> marked as a data dictionary
/// source, as seen by <c>DataDictionary.Generator</c>'s incremental pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Immutable and structurally equatable, per <c>research.md</c> §3: this is what
/// keeps the Roslyn incremental generator's cache warm. Because
/// <see cref="Members"/> is an <see cref="ImmutableArray{T}"/> — whose default
/// equality compares the underlying array reference, not its contents — this
/// record hand-rolls <see cref="Equals(EnumDictionaryModel?)"/> and
/// <see cref="GetHashCode"/> to compare every element structurally instead of
/// relying on the compiler-synthesized record equality.
/// </para>
/// </remarks>
/// <param name="EnumKey">
/// From <c>[DataDictionary("...")]</c> or the convention default (the enum's
/// simple name). Must be non-empty and unique across the compilation (diagnostic
/// DD0005 otherwise).
/// </param>
/// <param name="GroupName">Optional grouping label for the whole enum.</param>
/// <param name="ClrFullName">
/// The fully-qualified CLR type name, used for the optional catalog table and for
/// matching properties during <c>IsCodeInUseAsync</c>.
/// </param>
/// <param name="AssemblyName">The declaring assembly's simple name.</param>
/// <param name="Description">
/// The enum's own description, resolved the same way member descriptions are and
/// used for the catalog table.
/// </param>
/// <param name="IsFlags">
/// Whether the enum carries <c>[Flags]</c>. When <see langword="true"/>, this model
/// is never emitted into the manifest — diagnostic DD0007 fires instead.
/// </param>
/// <param name="Members">Every dictionary-eligible member of the enum.</param>
public sealed record EnumDictionaryModel(
    string EnumKey,
    string? GroupName,
    string ClrFullName,
    string AssemblyName,
    string? Description,
    bool IsFlags,
    ImmutableArray<DictionaryMemberModel> Members)
{
    /// <inheritdoc/>
    public bool Equals(EnumDictionaryModel? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EnumKey == other.EnumKey
            && GroupName == other.GroupName
            && ClrFullName == other.ClrFullName
            && AssemblyName == other.AssemblyName
            && Description == other.Description
            && IsFlags == other.IsFlags
            && Members.SequenceEqual(other.Members);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = HashCode.Seed;
        hash = HashCode.Combine(hash, EnumKey);
        hash = HashCode.Combine(hash, GroupName);
        hash = HashCode.Combine(hash, ClrFullName);
        hash = HashCode.Combine(hash, AssemblyName);
        hash = HashCode.Combine(hash, Description);
        hash = HashCode.Combine(hash, IsFlags);

        foreach (var member in Members)
        {
            hash = HashCode.Combine(hash, member);
        }

        return hash;
    }
}
