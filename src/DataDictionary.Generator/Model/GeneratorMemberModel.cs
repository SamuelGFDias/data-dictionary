using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Model;

/// <summary>
/// The generator's internal, incremental-pipeline mirror of
/// <c>DataDictionary.Abstractions.Manifest.DictionaryMemberModel</c> (see that type's
/// remarks — the generator cannot reference <c>DataDictionary.Abstractions</c> directly,
/// so it carries its own structurally-equivalent copy through the pipeline and only
/// emits the public <c>ManifestMemberEntry</c> shape as source text). Immutable and
/// structurally equatable per <c>research.md</c> §3; none of its fields are collections
/// so the compiler-synthesized record equality is sufficient — <see cref="Location"/>
/// carries Roslyn's own value-based <c>Equals</c>/<c>GetHashCode</c> (by source tree and
/// span), which is intentionally part of equality here: it is what lets the generator
/// point a diagnostic at the exact member declaration.
/// </summary>
/// <param name="FieldName">The C# member name.</param>
/// <param name="Code">
/// The resolved dictionary code, or <see langword="null"/> when no source resolved one
/// (DD0001 case — such a member is always excluded from the emitted manifest).
/// </param>
/// <param name="NumericValue">The member's underlying numeric value, widened to <see cref="long"/>.</param>
/// <param name="Description">The resolved description, or <see langword="null"/> when none resolved.</param>
/// <param name="GroupName">The member's group — always the owning enum's <c>GroupName</c>; see
/// <see cref="GeneratorEnumModel"/>'s remarks for why there is no per-member override.</param>
/// <param name="IsDeprecated">From <c>[DictionaryValue(Deprecated = ...)]</c>, default <see langword="false"/>.</param>
/// <param name="SortOrder">Declaration order within the enum.</param>
/// <param name="CodeSource">Which source resolved <paramref name="Code"/> — diagnostic aid only.</param>
/// <param name="Location">The member declaration's location, for diagnostic reporting.</param>
internal sealed record GeneratorMemberModel(
    string FieldName,
    string? Code,
    long NumericValue,
    string? Description,
    string? GroupName,
    bool IsDeprecated,
    int SortOrder,
    GeneratorCodeSource CodeSource,
    Location Location);
