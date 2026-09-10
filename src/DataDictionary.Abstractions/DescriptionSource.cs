namespace DataDictionary.Abstractions;

/// <summary>
/// Identifies where a <see cref="Manifest.DictionaryMemberModel.Description"/> (or
/// enum-level description) is resolved from, in the precedence order defined by
/// <c>spec.md</c> FR-005. Used as the value of
/// <see cref="DataDictionaryDefaultsAttribute.DescriptionFrom"/>.
/// </summary>
public enum DescriptionSource
{
    /// <summary>The member's XML documentation <c>&lt;summary&gt;</c>. Tried first.</summary>
    XmlDoc = 0,

    /// <summary>
    /// <see cref="System.ComponentModel.DescriptionAttribute"/>. Tried when no
    /// XML doc <c>&lt;summary&gt;</c> resolved.
    /// </summary>
    DescriptionAttribute,

    /// <summary>
    /// <c>System.ComponentModel.DataAnnotations.DisplayAttribute</c>'s <c>Name</c>
    /// property. Tried when neither of the above resolved.
    /// </summary>
    DisplayAttribute,

    /// <summary>The member's own C# name — the final fallback.</summary>
    MemberName,
}
