namespace DataDictionary.Abstractions.Manifest;

/// <summary>
/// Compile-time model of one member of a <see cref="EnumDictionaryModel"/>, as seen
/// by <c>DataDictionary.Generator</c>'s incremental pipeline. Immutable and
/// structurally equatable (a plain record works unmodified here because none of its
/// fields are collections — see <c>research.md</c> §3 and
/// <see cref="EnumDictionaryModel"/>, whose <c>Members</c> field of this type does
/// need the hand-rolled treatment).
/// </summary>
/// <param name="FieldName">
/// The C# member name; the second half of the persisted natural key
/// <c>(enum_key, field_name)</c>.
/// </param>
/// <param name="Code">
/// The resolved dictionary code, per the precedence in <c>spec.md</c> FR-005. Unique
/// within the owning enum (diagnostic DD0002) and within the configured maximum
/// code length (diagnostic DD0003).
/// </param>
/// <param name="NumericValue">
/// The member's underlying numeric value, widened to <see cref="long"/> to cover
/// every enum backing type.
/// </param>
/// <param name="Description">
/// The resolved description, per the precedence in <c>spec.md</c> FR-005.
/// <see langword="null"/> only when <c>RequireDescription</c> is
/// <see langword="false"/> and no source resolved (diagnostic DD0004 fires when
/// <c>RequireDescription</c> is <see langword="true"/> and this is
/// <see langword="null"/>).
/// </param>
/// <param name="GroupName">
/// The member's group. Falls back to the owning enum's <c>GroupName</c> when not
/// set per member.
/// </param>
/// <param name="IsDeprecated">
/// Whether this member is deprecated, from
/// <see cref="DictionaryValueAttribute.Deprecated"/>. Defaults to
/// <see langword="false"/>.
/// </param>
/// <param name="SortOrder">
/// The member's sort position. Defaults to its declaration order within the enum.
/// </param>
/// <param name="CodeSource">
/// Which source resolved <paramref name="Code"/> — a diagnostic/debugging aid, not
/// persisted.
/// </param>
public sealed record DictionaryMemberModel(
    string FieldName,
    string Code,
    long NumericValue,
    string? Description,
    string? GroupName,
    bool IsDeprecated,
    int SortOrder,
    CodeSource CodeSource);
