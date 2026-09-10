using System.Collections.Immutable;
using DataDictionary.Generator.Internal;
using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Model;

/// <summary>
/// The generator's internal, incremental-pipeline mirror of
/// <c>DataDictionary.Abstractions.Manifest.EnumDictionaryModel</c>. The generator
/// project deliberately does not reference <c>DataDictionary.Abstractions</c> (per
/// <c>DataDictionary.Generator.csproj</c> — it only ever emits fully-qualified source
/// text against the consumer's own reference to that package), so it cannot flow actual
/// <c>EnumDictionaryModel</c>/<c>DictionaryMemberModel</c> instances through its
/// pipeline; this type and <see cref="GeneratorMemberModel"/> are its own
/// structurally-equivalent stand-ins, carrying one extra field
/// (<see cref="Location"/>, for diagnostics) that the public contract type does not
/// need.
/// </summary>
/// <remarks>
/// Immutable and structurally equatable per <c>research.md</c> §3: because
/// <see cref="Members"/> is an <see cref="ImmutableArray{T}"/> (whose default equality
/// is reference-based, not content-based), this record hand-rolls
/// <see cref="Equals(GeneratorEnumModel?)"/> and <see cref="GetHashCode"/> to compare
/// every element structurally instead of relying on compiler-synthesized record
/// equality — otherwise the incremental cache would invalidate on every edit even when
/// the resolved members did not actually change.
/// </remarks>
/// <param name="EnumKey">From <c>[DataDictionary("...")]</c> or the enum's own simple name (convention mode).</param>
/// <param name="GroupName">Optional grouping label for the whole enum.</param>
/// <param name="ClrFullName">The fully-qualified CLR type name.</param>
/// <param name="AssemblyName">The declaring assembly's simple name.</param>
/// <param name="Description">The enum's own resolved description.</param>
/// <param name="IsFlags">
/// Whether the enum carries <c>[Flags]</c>. A model with <see langword="true"/> here is
/// never produced by <c>DictionaryModelBuilder</c> — DD0007 fires and the enum is
/// dropped before a model is built — but the field is kept for parity with the public
/// contract type and for any future diagnostic that wants to inspect it.
/// </param>
/// <param name="Members">Every dictionary-eligible member of the enum that survived validation.</param>
/// <param name="Location">The enum declaration's location, for diagnostic reporting.</param>
internal sealed record GeneratorEnumModel(
    string EnumKey,
    string? GroupName,
    string ClrFullName,
    string AssemblyName,
    string? Description,
    bool IsFlags,
    ImmutableArray<GeneratorMemberModel> Members,
    Location Location)
{
    /// <inheritdoc/>
    public bool Equals(GeneratorEnumModel? other)
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
            && Location.Equals(other.Location)
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
        hash = HashCode.Combine(hash, Location);

        foreach (var member in Members)
        {
            hash = HashCode.Combine(hash, member);
        }

        return hash;
    }
}
