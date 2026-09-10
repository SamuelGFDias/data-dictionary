namespace DataDictionary.Abstractions;

/// <summary>
/// Identifies where a <see cref="Manifest.DictionaryMemberModel.Code"/> value was
/// resolved from. Used both as the value of
/// <see cref="DataDictionaryDefaultsAttribute.CodeSource"/> (the
/// assembly-level convention to apply when no explicit
/// <see cref="DictionaryValueAttribute"/> is present) and as
/// <see cref="Manifest.DictionaryMemberModel.CodeSource"/> (the diagnostic record of
/// which source actually won for a given member), per the precedence order in
/// <c>spec.md</c> FR-005.
/// </summary>
public enum CodeSource
{
    /// <summary>
    /// Set explicitly via <see cref="DictionaryValueAttribute"/>. Always
    /// takes precedence over every convention-derived source.
    /// </summary>
    Explicit = 0,

    /// <summary>Derived from the member's XML documentation <c>&lt;summary&gt;</c>.</summary>
    XmlDoc,

    /// <summary>
    /// Derived from <see cref="System.ComponentModel.DescriptionAttribute"/>.
    /// </summary>
    DescriptionAttribute,

    /// <summary>
    /// Derived from <c>System.ComponentModel.DataAnnotations.DisplayAttribute</c>'s
    /// <c>Name</c> property.
    /// </summary>
    DisplayAttribute,

    /// <summary>Derived from the member's own C# name — the final fallback.</summary>
    MemberName,
}
