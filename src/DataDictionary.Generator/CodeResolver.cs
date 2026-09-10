using DataDictionary.Generator.Internal;
using DataDictionary.Generator.Model;
using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator;

/// <summary>
/// Resolves a convention-governed (scanned) member's dictionary code from the single
/// source configured by <c>[assembly: DataDictionaryDefaults(CodeSource = ...)]</c> —
/// unlike <see cref="DescriptionResolver"/>, this is a single lookup, not a cascading
/// chain: <c>DataDictionaryDefaultsAttribute.CodeSource</c>'s own XML doc describes it as
/// "how a member's dictionary code is derived", with no reference to FR-005's precedence
/// language, and <c>contracts/attributes-contract.md</c> confirms "<c>MemberName</c> is
/// the only value exercised by the spec's examples" — i.e. a single named source, not a
/// fallback list. This mirrors <see cref="DescriptionResolver"/> only in reusing the same
/// raw-source extraction helpers (<see cref="SymbolTextExtensions"/>), never in its
/// cascading behavior.
/// </summary>
internal static class CodeResolver
{
    /// <summary>
    /// Resolves <paramref name="member"/>'s code from the single configured
    /// <paramref name="source"/>, or <see langword="null"/> when that source did not
    /// resolve anything (the caller reports DD0001 in that case).
    /// </summary>
    internal static string? ResolveFromConventionSource(IFieldSymbol member, GeneratorCodeSource source) => source switch
    {
        GeneratorCodeSource.MemberName => member.Name,
        GeneratorCodeSource.XmlDoc => member.GetXmlDocSummary(),
        GeneratorCodeSource.DescriptionAttribute => member.GetDescriptionAttributeValue(),
        GeneratorCodeSource.DisplayAttribute => member.GetDisplayNameValue(),
        // GeneratorCodeSource.Explicit has no meaning as a convention fallback — it
        // means "an explicit [DictionaryValue] is required", which by construction is
        // absent whenever this method is even called. Resolves to "nothing", surfacing
        // as DD0001 like any other unresolved source.
        _ => null,
    };
}
