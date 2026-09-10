namespace DataDictionary.Abstractions;

/// <summary>
/// Assembly-level convention configuration for enums picked up via
/// <see cref="DataDictionaryScanAttribute"/> without an explicit per-member
/// <see cref="DictionaryValueAttribute"/>. See
/// <c>contracts/attributes-contract.md</c>'s "Assembly-level convention mode"
/// example.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class DataDictionaryDefaultsAttribute : Attribute
{
    /// <summary>
    /// How a member's dictionary code is derived when no explicit
    /// <see cref="DictionaryValueAttribute"/> is present. Defaults to
    /// <see cref="CodeSource.MemberName"/>.
    /// </summary>
    public CodeSource CodeSource { get; set; } = CodeSource.MemberName;

    /// <summary>
    /// How a member's (or the owning enum's) description is resolved, per the
    /// precedence in <c>spec.md</c> FR-005. Defaults to <see cref="DescriptionSource.XmlDoc"/>.
    /// </summary>
    public DescriptionSource DescriptionFrom { get; set; } = DescriptionSource.XmlDoc;

    /// <summary>
    /// When <see langword="true"/>, a member for which no description source
    /// resolved is a diagnostic error (DD0004) instead of persisting with a
    /// <see langword="null"/> description. Defaults to <see langword="false"/>.
    /// </summary>
    public bool RequireDescription { get; set; }

    /// <summary>
    /// The maximum length, in characters, a resolved <c>code</c> may have before the
    /// DD0003 diagnostic fires. Applies to every member in the compilation — explicit
    /// mode and convention mode alike, since this is an assembly-level setting rather
    /// than a per-mode one. Defaults to <c>64</c>.
    /// </summary>
    public int MaxCodeLength { get; set; } = 64;
}
