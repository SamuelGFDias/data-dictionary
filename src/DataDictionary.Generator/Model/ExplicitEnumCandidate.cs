using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Model;

/// <summary>
/// One enum declaration decorated with <c>[DataDictionary("...")]</c>, as captured by
/// the <c>ForAttributeWithMetadataName</c> registration for that attribute (explicit
/// mode's entry point — <c>research.md</c> §3). Carries the raw <see cref="AttributeData"/>
/// rather than the already-resolved <c>EnumKey</c>/<c>Group</c> so
/// <see cref="DataDictionary.Generator.DictionaryModelBuilder"/> can read them alongside
/// every other symbol-derived value in one place.
/// </summary>
/// <param name="Symbol">The decorated enum's symbol.</param>
/// <param name="Attribute">The single <c>[DataDictionary]</c> attribute application (AllowMultiple = false).</param>
internal sealed record ExplicitEnumCandidate(INamedTypeSymbol Symbol, AttributeData Attribute)
{
    /// <inheritdoc/>
    public bool Equals(ExplicitEnumCandidate? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return SymbolEqualityComparer.Default.Equals(Symbol, other.Symbol)
            && Attribute.Equals(other.Attribute);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(Symbol);
}
