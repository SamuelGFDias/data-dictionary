namespace DataDictionary.Generator.Model;

/// <summary>
/// The generator's own internal mirror of <c>DataDictionary.Abstractions.CodeSource</c>
/// (member names kept identical on purpose). The generator project cannot reference
/// <c>DataDictionary.Abstractions</c> — it only ever emits fully-qualified source text
/// against it — so it cannot use the public enum directly; this internal copy exists
/// purely for the generator's own diagnostic bookkeeping and is never emitted into
/// generated source (the emitted <c>ManifestMemberEntry</c> has no <c>CodeSource</c>
/// field at all, per <c>data-model.md</c>).
/// </summary>
internal enum GeneratorCodeSource
{
    /// <summary>Set explicitly via <c>[DictionaryValue]</c>.</summary>
    Explicit = 0,

    /// <summary>Derived from the member's XML documentation <c>&lt;summary&gt;</c>.</summary>
    XmlDoc,

    /// <summary>Derived from <see cref="System.ComponentModel.DescriptionAttribute"/>.</summary>
    DescriptionAttribute,

    /// <summary>
    /// Derived from <c>System.ComponentModel.DataAnnotations.DisplayAttribute</c>'s
    /// <c>Name</c> property.
    /// </summary>
    DisplayAttribute,

    /// <summary>Derived from the member's own C# name.</summary>
    MemberName,
}
