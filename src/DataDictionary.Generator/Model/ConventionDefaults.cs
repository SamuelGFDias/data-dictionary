namespace DataDictionary.Generator.Model;

/// <summary>
/// The parsed contents of a single assembly-level
/// <c>[assembly: DataDictionaryDefaults(...)]</c> attribute (<c>AllowMultiple = false</c>
/// on the source attribute, so at most one of these exists per compilation). Immutable
/// and structurally equatable — a plain record works unmodified since every field is a
/// primitive/enum.
/// </summary>
/// <param name="CodeSource">
/// How a member's dictionary code is derived when no explicit <c>[DictionaryValue]</c>
/// is present, for enums reached through <c>[assembly: DataDictionaryScan]</c>. Default
/// on the source attribute is <c>MemberName</c>.
/// </param>
/// <param name="RequireDescription">
/// When <see langword="true"/>, a scanned member for which none of the XML
/// doc/<c>[Description]</c>/<c>[Display]</c> sources resolved is a diagnostic warning
/// (DD0004) instead of silently falling back to the member's own C# name — see
/// <see cref="DataDictionary.Generator.DescriptionResolver"/>'s remarks for why this
/// flag suppresses the final fallback tier rather than merely gating a diagnostic.
/// </param>
internal readonly record struct ConventionDefaults(GeneratorCodeSource CodeSource, bool RequireDescription)
{
    /// <summary>
    /// The effective defaults used when a scanned assembly declares
    /// <c>[assembly: DataDictionaryScan(...)]</c> without an accompanying
    /// <c>[assembly: DataDictionaryDefaults(...)]</c> — mirrors the source attribute's
    /// own declared default property values (<c>CodeSource = CodeSource.MemberName</c>,
    /// <c>RequireDescription = false</c>).
    /// </summary>
    internal static ConventionDefaults Default { get; } = new(GeneratorCodeSource.MemberName, RequireDescription: false);
}
