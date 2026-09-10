using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace DataDictionary.Generator.Internal;

/// <summary>
/// Turns an assembly's simple name into a valid C# namespace path for the generated
/// <c>&lt;ConsumerAssembly&gt;.Generated</c> namespace
/// (<c>contracts/generated-entrypoints-contract.md</c>). Most assembly names are already
/// valid namespace paths (e.g. <c>"MyCompany.MyApp"</c>); this only kicks in for the
/// uncommon case of an assembly name containing characters that are not valid in a C#
/// identifier (e.g. <c>"My-App"</c>, or a name starting with a digit).
/// </summary>
internal static class NamespaceSanitizer
{
    internal static string ToNamespaceSegment(string assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
        {
            return "GeneratedDataDictionary";
        }

        var segments = assemblyName.Split('.');
        var sanitizedSegments = new string[segments.Length];

        for (var i = 0; i < segments.Length; i++)
        {
            sanitizedSegments[i] = SanitizeSegment(segments[i]);
        }

        return string.Join(".", sanitizedSegments);
    }

    private static string SanitizeSegment(string segment)
    {
        if (SyntaxFacts.IsValidIdentifier(segment))
        {
            return segment;
        }

        var builder = new StringBuilder(segment.Length + 1);

        foreach (var c in segment)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('_');
            }
        }

        if (builder.Length == 0 || !SyntaxFacts.IsIdentifierStartCharacter(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }
}
