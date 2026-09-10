using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
    /// <remarks>
    /// <para>
    /// <b>Why the syntax fallback exists.</b> <see cref="ISymbol.GetDocumentationCommentXml"/>
    /// only returns anything when the consumer's syntax trees were parsed with
    /// <see cref="DocumentationMode"/> <c>Parse</c>/<c>Diagnose</c>. The C# command-line
    /// compiler selects <see cref="DocumentationMode.None"/> whenever no <c>/doc:</c>
    /// switch is passed — i.e. for every project that does not set
    /// <c>&lt;GenerateDocumentationFile&gt;true&lt;/GenerateDocumentationFile&gt;</c>,
    /// which is the SDK default for non-library projects. Under that mode a <c>///</c>
    /// comment is still in the tree, but as ordinary
    /// <see cref="SyntaxKind.SingleLineCommentTrivia"/> rather than structured
    /// documentation trivia, and the API above returns an empty string.
    /// </para>
    /// <para>
    /// Relying on the API alone therefore made the FR-005 precedence chain skip its own
    /// first tier without any diagnostic and silently fall through to the member-name
    /// tier. That failure is invisible for members whose summary merely repeats the
    /// identifier (<c>/// &lt;summary&gt;Branca&lt;/summary&gt;</c> on <c>Branca</c>) and
    /// shows up as quietly wrong persisted data for every member whose summary differs
    /// from it — most visibly by dropping diacritics a C# identifier cannot carry
    /// (<c>Indígena</c> resolving to the member name <c>Indigena</c>). Reading the raw
    /// <c>///</c> trivia back makes the tier work under every
    /// <see cref="DocumentationMode"/>, so the generator's output no longer depends on a
    /// consumer MSBuild property it does not control.
    /// </para>
    /// <para>
    /// The API is still tried first: it resolves <c>&lt;include&gt;</c> files
    /// (<c>expandIncludes: true</c>) and partial-declaration merging, which raw trivia
    /// cannot. The fallback only runs when the API produced nothing.
    /// </para>
    /// </remarks>
    internal static string? GetXmlDocSummary(this ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml(expandIncludes: true);

        return ExtractSummary(xml) ?? ExtractSummary(ReadDocCommentFromTrivia(symbol));
    }

    /// <summary>
    /// Parses a <c>&lt;member&gt;</c>-rooted documentation XML fragment and returns its
    /// <c>&lt;summary&gt;</c> element's trimmed inner text, or <see langword="null"/>
    /// when the fragment is absent, malformed, or carries no non-empty summary.
    /// </summary>
    private static string? ExtractSummary(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            // The fragment looks like
            // "<member name="F:...">\n  <summary>\n  Text\n  </summary>\n</member>",
            // which XElement.Parse accepts directly since it is already well-formed XML.
            var root = XElement.Parse(xml!);
            var summary = root.Element("summary");
            var text = summary?.Value;
            return string.IsNullOrWhiteSpace(text) ? null : CollapseWhitespace(text!);
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
    /// Trims the text and collapses every interior run of whitespace (including line
    /// breaks and the per-line indentation a multi-line <c>///</c> block carries) to a
    /// single space, or returns <see langword="null"/> when nothing but whitespace is
    /// left.
    /// </summary>
    /// <remarks>
    /// A description is a single-line label persisted into a database column, not
    /// formatted prose, so its line breaks carry no meaning. Normalizing them also keeps
    /// the two extraction paths in <see cref="GetXmlDocSummary"/> byte-identical: the
    /// compiler re-indents the inner text of a multi-line summary to four spaces per
    /// line, while the raw-trivia fallback sees whatever the author typed after
    /// <c>///</c>. Without this, the same source would produce two different
    /// descriptions — and therefore two different content hashes, i.e. spurious dictionary
    /// churn — depending only on whether the consumer sets
    /// <c>&lt;GenerateDocumentationFile&gt;</c>.
    /// </remarks>
    private static string? CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>
    /// Rebuilds a <c>&lt;member&gt;</c>-rooted documentation XML fragment out of the raw
    /// <c>///</c> comment trivia leading the symbol's declaration, for compilations
    /// parsed with <see cref="DocumentationMode.None"/> (where
    /// <see cref="ISymbol.GetDocumentationCommentXml"/> yields nothing). Returns
    /// <see langword="null"/> when the symbol has no C# declaration in source or no
    /// <c>///</c> trivia leads it.
    /// </summary>
    private static string? ReadDocCommentFromTrivia(ISymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var builder = new StringBuilder();

            foreach (var trivia in reference.GetSyntax().GetLeadingTrivia())
            {
                // Under DocumentationMode.None a "///" comment is plain single-line
                // comment trivia; under Parse/Diagnose it is structured documentation
                // trivia and this method is never reached for that symbol.
                if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                {
                    continue;
                }

                var text = trivia.ToString();

                // Only "///" starts a documentation comment: "//" is an ordinary comment
                // and "////" is explicitly *not* a documentation comment in C#.
                if (text.Length < 3 || text[0] != '/' || text[1] != '/' || text[2] != '/')
                {
                    continue;
                }

                if (text.Length > 3 && text[3] == '/')
                {
                    continue;
                }

                builder.Append(text, 3, text.Length - 3).Append('\n');
            }

            if (builder.Length == 0)
            {
                continue;
            }

            // Wrap in the same <member> root GetDocumentationCommentXml would have
            // produced, so ExtractSummary sees one shape regardless of the source.
            return "<member>\n" + builder.ToString() + "</member>";
        }

        return null;
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
