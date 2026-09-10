using DataDictionary.Generator.Internal;
using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator;

/// <summary>
/// Implements the description-precedence resolution mandated by FR-005 and
/// <c>contracts/attributes-contract.md</c>'s "Description precedence" section: XML
/// documentation <c>&lt;summary&gt;</c>, then <c>[Description]</c>, then
/// <c>[Display(Name = ...)]</c>, then — normally — the symbol's own C# name as the
/// final, always-succeeding fallback.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the final fallback tier is conditional.</b> Taken completely literally,
/// FR-005's fixed four-tier chain can never leave a description unresolved (a symbol
/// always has a name), which would make DD0004 ("no resolvable description with
/// RequireDescription=true") permanently unreachable — yet the diagnostics contract
/// requires a real positive test case for it, and <c>tasks.md</c> ties DD0004's
/// implementation to T024 (convention mode), not directly to this resolver's own task
/// (T023). The reconciling reading this generator implements: the fourth tier (member
/// name as description) is a convenience fallback for the common case, suppressed only
/// when the caller explicitly opts into <c>RequireDescription = true</c> — at that
/// point a bare repeat of the C# identifier no longer counts as "a real description",
/// so resolution stops after <c>[Display]</c> and reports <see langword="null"/>
/// instead, letting DD0004 fire.
/// </para>
/// <para>
/// <b>Why <c>DataDictionaryDefaultsAttribute.DescriptionFrom</c> is not consulted
/// here.</b> Its own XML doc says it selects "how a member's ... description is
/// resolved, per the precedence in spec.md FR-005" — i.e. it names a point in this same
/// fixed chain, not an alternative algorithm — and, like
/// <c>DataDictionaryDefaultsAttribute.CodeSource</c> ("<c>MemberName</c> is the only
/// value exercised by the spec's examples"), every example in
/// <c>contracts/attributes-contract.md</c> only ever sets it to its default value
/// (<c>DescriptionSource.XmlDoc</c>) — the same tier this resolver already starts at
/// unconditionally. Until a scenario needs it to mean something else, this generator
/// treats it as a forward-compatible, currently-inert configuration surface rather than
/// inventing an unvalidated "skip to this tier" behavior; this is a scope decision worth
/// confirming with the product owner if convention-mode consumers ever need to start the
/// chain somewhere other than XML doc.
/// </para>
/// </remarks>
internal static class DescriptionResolver
{
    /// <summary>
    /// Resolves <paramref name="symbol"/>'s description via the FR-005 precedence
    /// chain.
    /// </summary>
    /// <param name="symbol">The enum or enum-member symbol to resolve a description for.</param>
    /// <param name="allowMemberNameFallback">
    /// Whether the final "symbol's own name" tier may be used. Pass
    /// <see langword="false"/> only for convention-governed (scanned) symbols under
    /// <c>RequireDescription = true</c> — see this type's remarks.
    /// </param>
    /// <returns>The resolved description, or <see langword="null"/> when nothing resolved.</returns>
    internal static string? Resolve(ISymbol symbol, bool allowMemberNameFallback)
    {
        var xmlDoc = symbol.GetXmlDocSummary();
        if (xmlDoc is not null)
        {
            return xmlDoc;
        }

        var description = symbol.GetDescriptionAttributeValue();
        if (description is not null)
        {
            return description;
        }

        var displayName = symbol.GetDisplayNameValue();
        if (displayName is not null)
        {
            return displayName;
        }

        return allowMemberNameFallback ? symbol.Name : null;
    }
}
