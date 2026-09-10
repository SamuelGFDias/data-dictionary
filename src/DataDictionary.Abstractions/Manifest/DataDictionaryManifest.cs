using System.Collections.Immutable;
using DataDictionary.Abstractions.Internal;

namespace DataDictionary.Abstractions.Manifest;

/// <summary>
/// The flattened, runtime-facing projection of every <see cref="EnumDictionaryModel"/>
/// that passed validation in a given compilation. Built entirely from compile-time-known
/// literal data by the generated <c>&lt;ConsumerAssembly&gt;.Generated.DataDictionaryManifest.Default</c>
/// static (see <c>contracts/generated-entrypoints-contract.md</c>) — no reflection,
/// attribute inspection, or assembly scanning occurs when that static member is
/// initialized.
/// </summary>
/// <remarks>
/// Immutable and structurally equatable, per <c>research.md</c> §3 — the manifest
/// model is part of the generator's incremental pipeline just as
/// <see cref="EnumDictionaryModel"/> is, so it hand-rolls
/// <see cref="Equals(DataDictionaryManifest?)"/> and <see cref="GetHashCode"/> to
/// compare <see cref="Enums"/> structurally instead of relying on
/// <see cref="ImmutableArray{T}"/>'s reference-based default equality.
/// </remarks>
/// <param name="Enums">Every enum that passed validation, in this manifest.</param>
public sealed record DataDictionaryManifest(ImmutableArray<ManifestEnumEntry> Enums)
{
    /// <inheritdoc/>
    public bool Equals(DataDictionaryManifest? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Enums.SequenceEqual(other.Enums);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = HashCode.Seed;

        foreach (var entry in Enums)
        {
            hash = HashCode.Combine(hash, entry);
        }

        return hash;
    }
}

/// <summary>
/// One enum's entry in a <see cref="DataDictionaryManifest"/> — the same fields as
/// <see cref="EnumDictionaryModel"/>, minus nothing (the generator-only
/// diagnostic field lives one level down, on <see cref="ManifestMemberEntry"/>).
/// </summary>
/// <remarks>
/// Immutable and structurally equatable for the same reason as
/// <see cref="EnumDictionaryModel"/> — see its remarks and <c>research.md</c> §3.
/// </remarks>
/// <param name="EnumKey">The enum's stable dictionary identifier.</param>
/// <param name="GroupName">Optional grouping label for the whole enum.</param>
/// <param name="ClrFullName">The fully-qualified CLR type name.</param>
/// <param name="AssemblyName">The declaring assembly's simple name.</param>
/// <param name="Description">The enum's own resolved description.</param>
/// <param name="IsFlags">Whether the enum carries <c>[Flags]</c>.</param>
/// <param name="Members">Every dictionary-eligible member of the enum.</param>
public sealed record ManifestEnumEntry(
    string EnumKey,
    string? GroupName,
    string ClrFullName,
    string AssemblyName,
    string? Description,
    bool IsFlags,
    ImmutableArray<ManifestMemberEntry> Members)
{
    /// <inheritdoc/>
    public bool Equals(ManifestEnumEntry? other)
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

/// <summary>
/// One member's entry in a <see cref="ManifestEnumEntry"/> — the same fields as
/// <see cref="DictionaryMemberModel"/>, minus its generator-only
/// <see cref="DictionaryMemberModel.CodeSource"/> diagnostic field, per
/// <c>data-model.md</c>'s <c>DataDictionaryManifest</c> section.
/// </summary>
/// <param name="FieldName">
/// The C# member name; the second half of the persisted natural key
/// <c>(enum_key, field_name)</c>.
/// </param>
/// <param name="Code">The resolved dictionary code.</param>
/// <param name="NumericValue">
/// The member's underlying numeric value, widened to <see cref="long"/>.
/// </param>
/// <param name="Description">The member's resolved description, if any.</param>
/// <param name="GroupName">The member's group, if any.</param>
/// <param name="IsDeprecated">Whether this member is deprecated.</param>
/// <param name="SortOrder">The member's sort position.</param>
public sealed record ManifestMemberEntry(
    string FieldName,
    string Code,
    long NumericValue,
    string? Description,
    string? GroupName,
    bool IsDeprecated,
    int SortOrder);
