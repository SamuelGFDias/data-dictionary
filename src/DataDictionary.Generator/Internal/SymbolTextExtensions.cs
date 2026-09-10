using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Internal;

/// <summary>
/// Shared, single-source-of-truth extraction of the three raw text sources every
/// description/code resolution strategy draws from: the symbol's XML documentation
/// <c>&lt;summary&gt;</c>, <c>System.ComponentModel.DescriptionAttribute</c>, and
/// <c>System.ComponentModel.DataAnnotations.DisplayAttribute</c>'s <c>Name</c>. Used by
/// both <see cref="DataDictionary.Generator.DescriptionResolver"/> (the fixed FR-005
/// precedence chain) and <see cref="DataDictionary.Generator.CodeResolver"/> (a single
/// convention-configured source) so the two never disagree on how a given source is
/// read from the symbol.
/// </summary>
internal static class SymbolTextExtensions
{
    private const string DescriptionAttributeMetadataName = "System.ComponentModel.DescriptionAttribute";
    private const string DisplayAttributeMetadataName = "System.ComponentModel.DataAnnotations.DisplayAttribute";

    /// <summary>
    /// Reads the trimmed inner text of the symbol's XML documentation
    /// <c>&lt;summary&gt;</c> element, or <see langword="null"/> when the symbol has no
    /// documentation comment, no <c>&lt;summary&gt;</c> element, or the element is
    /// empty/whitespace-only.
    /// </summary>
    internal static string? GetXmlDocSummary(this ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml(expandIncludes: true);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            // GetDocumentationCommentXml returns a fragment such as
            // "<member name="F:...">\n  <summary>\n  Text\n  </summary>\n</member>",
            // which XElement.Parse accepts directly since it is already well-formed XML.
            var root = XElement.Parse(xml!);
            var summary = root.Element("summary");
            var text = summary?.Value;
            return string.IsNullOrWhiteSpace(text) ? null : text!.Trim();
        }
        catch (System.Xml.XmlException)
        {
            // Malformed doc comments (e.g. unclosed tags) should never crash the
            // generator; treat them as "no summary resolved" and let the precedence
            // chain fall through to the next source.
            return null;
        }
    }

    /// <summary>
    /// Reads <see cref="System.ComponentModel.DescriptionAttribute"/>'s constructor
    /// argument, or <see langword="null"/> when the attribute is absent or its value is
    /// empty/whitespace-only.
    /// </summary>
    internal static string? GetDescriptionAttributeValue(this ISymbol symbol)
    {
        var attribute = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DescriptionAttributeMetadataName);

        if (attribute is null || attribute.ConstructorArguments.Length == 0)
        {
            return null;
        }

        var value = attribute.ConstructorArguments[0].Value as string;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Reads <c>System.ComponentModel.DataAnnotations.DisplayAttribute</c>'s
    /// <c>Name</c> named argument, or <see langword="null"/> when the attribute is
    /// absent, <c>Name</c> was not set, or its value is empty/whitespace-only.
    /// </summary>
    internal static string? GetDisplayNameValue(this ISymbol symbol)
    {
        var attribute = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DisplayAttributeMetadataName);

        if (attribute is null)
        {
            return null;
        }

        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == "Name" && named.Value.Value is string value)
            {
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }
}
