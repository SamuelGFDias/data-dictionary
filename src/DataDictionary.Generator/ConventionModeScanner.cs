using System.Collections.Immutable;
using DataDictionary.Generator.Model;
using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator;

/// <summary>
/// Implements convention-mode scanning: reading
/// <c>[assembly: DataDictionaryDefaults]</c> / <c>[assembly: DataDictionaryScan]</c>
/// (FR-003, FR-004) and deciding which enum declarations in the compilation they make
/// dictionary-eligible without requiring a per-enum <c>[DataDictionary]</c> attribute.
/// </summary>
internal static class ConventionModeScanner
{
    private const string CodeSourcePropertyName = "CodeSource";
    private const string RequireDescriptionPropertyName = "RequireDescription";
    private const string MaxCodeLengthPropertyName = "MaxCodeLength";

    /// <summary>
    /// Reads a single <c>[assembly: DataDictionaryDefaults(...)]</c> application's
    /// <c>CodeSource</c>, <c>RequireDescription</c> and <c>MaxCodeLength</c> named
    /// arguments (named arguments not present on the attribute application keep the
    /// source attribute's own declared default: <c>CodeSource.MemberName</c>,
    /// <c>RequireDescription = false</c>, <c>MaxCodeLength = 64</c> — <c>DescriptionFrom</c>
    /// is intentionally not read here, see <see cref="DescriptionResolver"/>'s remarks).
    /// </summary>
    internal static ConventionDefaults ReadDefaults(AttributeData attribute)
    {
        var codeSource = GeneratorCodeSource.MemberName;
        var requireDescription = false;
        var maxCodeLength = GeneratorConstants.DefaultMaxCodeLength;

        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == CodeSourcePropertyName && named.Value.Value is int codeSourceValue)
            {
                // DataDictionary.Abstractions.CodeSource and GeneratorCodeSource declare
                // their members in the same order (Explicit, XmlDoc,
                // DescriptionAttribute, DisplayAttribute, MemberName), so the underlying
                // int value maps directly.
                codeSource = (GeneratorCodeSource)codeSourceValue;
            }
            else if (named.Key == RequireDescriptionPropertyName && named.Value.Value is bool requireDescriptionValue)
            {
                requireDescription = requireDescriptionValue;
            }
            else if (named.Key == MaxCodeLengthPropertyName && named.Value.Value is int maxCodeLengthValue)
            {
                maxCodeLength = maxCodeLengthValue;
            }
        }

        return new ConventionDefaults(codeSource, requireDescription, maxCodeLength);
    }

    /// <summary>
    /// Reads every <c>[assembly: DataDictionaryScan("prefix")]</c> application's
    /// namespace-prefix constructor argument.
    /// </summary>
    internal static ImmutableArray<string> ReadScanPrefixes(ImmutableArray<AttributeData> attributes)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return ImmutableArray<string>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<string>(attributes.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var attribute in attributes)
        {
            if (attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is string prefix
                && !string.IsNullOrEmpty(prefix)
                && seen.Add(prefix))
            {
                builder.Add(prefix);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Whether <paramref name="enumSymbol"/>'s containing namespace is covered by any of
    /// <paramref name="scanPrefixes"/> — an exact match, or a nested namespace under the
    /// prefix (<c>"MyApp.Domain.Enums"</c> covers <c>"MyApp.Domain.Enums.Sub"</c> too).
    /// </summary>
    internal static bool IsInScannedNamespace(INamedTypeSymbol enumSymbol, ImmutableArray<string> scanPrefixes)
    {
        if (scanPrefixes.IsDefaultOrEmpty)
        {
            return false;
        }

        var containingNamespace = enumSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        foreach (var prefix in scanPrefixes)
        {
            if (containingNamespace == prefix
                || containingNamespace.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
