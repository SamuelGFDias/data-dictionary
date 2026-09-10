using Microsoft.CodeAnalysis;
using Xunit;

namespace DataDictionary.Generator.Tests;

/// <summary>
/// Regression coverage for the FR-005 precedence chain's first tier (XML documentation
/// <c>&lt;summary&gt;</c>) under both <see cref="DocumentationMode"/>s a real consumer
/// build can use.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these tests pin.</b> The XML-doc tier was implemented purely on
/// <see cref="ISymbol.GetDocumentationCommentXml"/>, which returns an empty string
/// whenever the compilation's syntax trees were parsed with
/// <see cref="DocumentationMode.None"/>. The C# compiler picks exactly that mode when no
/// <c>/doc:</c> switch is passed — i.e. for every project without
/// <c>&lt;GenerateDocumentationFile&gt;true&lt;/GenerateDocumentationFile&gt;</c>, the
/// SDK default for application projects such as <c>samples/Sample.Api</c>.
/// </para>
/// <para>
/// The tier therefore resolved nothing and the chain silently fell through to its
/// member-name tier, with no diagnostic. That is invisible for a member whose summary
/// merely repeats its identifier (<c>Branca</c>), so the failure only surfaced through
/// the one member whose summary cannot equal its identifier: a C# name cannot carry a
/// diacritic, so <c>/// &lt;summary&gt;Indígena&lt;/summary&gt;</c> on a member named
/// <c>Indigena</c> reached the database as the unaccented member name and read as
/// "corrupted encoding" rather than as a skipped resolution tier.
/// </para>
/// <para>
/// The accented fixture below is the load-bearing part of these tests: an ASCII-only
/// summary would pass against the very bug they exist to catch.
/// </para>
/// </remarks>
public class XmlDocSummaryResolutionTests
{
    private const string AccentedSource = """
        using DataDictionary.Abstractions;

        namespace Fixture.XmlDoc;

        /// <summary>Raça/cor declarada pelo cidadão.</summary>
        [DataDictionary("RacaCor")]
        public enum RacaCor
        {
            /// <summary>Branca</summary>
            [DictionaryValue("B")]
            Branca = 1,

            /// <summary>Indígena</summary>
            [DictionaryValue("I")]
            Indigena = 5,
        }
        """;

    [Theory]
    [InlineData(DocumentationMode.None)]
    [InlineData(DocumentationMode.Parse)]
    [InlineData(DocumentationMode.Diagnose)]
    public void XmlDocSummary_ResolvesAccentedText_UnderEveryDocumentationMode(
        DocumentationMode documentationMode)
    {
        var output = GeneratorTestHelper.Run(documentationMode, AccentedSource);

        Assert.Contains("Description: \"Indígena\"", output, StringComparison.Ordinal);
        Assert.Contains("Description: \"Raça/cor declarada pelo cidadão.\"", output, StringComparison.Ordinal);

        // The member-name fallback tier is what the resolver silently used when the
        // XML-doc tier came back empty, so its output is the exact signature of the
        // regression: "Indigena" is the C# identifier, never the documented description.
        Assert.DoesNotContain("Description: \"Indigena\"", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The generated manifest must not depend on the consumer's
    /// <c>&lt;GenerateDocumentationFile&gt;</c> setting: the same sources must emit
    /// byte-identical output whether or not the compiler was asked for a doc file.
    /// </summary>
    /// <remarks>
    /// This covers more than the empty-vs-populated case: the compiler re-indents the
    /// inner text of a multi-line <c>&lt;summary&gt;</c> to four spaces per line, while
    /// reading the raw <c>///</c> trivia sees the author's own indentation. Left
    /// unnormalized, that difference alone would change a description's content hash and
    /// make an unrelated build-property flip look like a dictionary change.
    /// </remarks>
    [Fact]
    public void GeneratedManifest_IsIdentical_WithAndWithoutDocumentationFile()
    {
        const string multiLineSource = """
            using DataDictionary.Abstractions;

            namespace Fixture.XmlDoc;

            /// <summary>
            /// Raça/cor declarada pelo cidadão, conforme o
            /// cadastro nacional.
            /// </summary>
            [DataDictionary("RacaCor")]
            public enum RacaCor
            {
                /// <summary>
                /// Indígena — descrição em várias linhas.
                /// </summary>
                [DictionaryValue("I")]
                Indigena = 5,
            }
            """;

        var withoutDocFile = GeneratorTestHelper.Run(DocumentationMode.None, multiLineSource);
        var withDocFile = GeneratorTestHelper.Run(DocumentationMode.Diagnose, multiLineSource);

        Assert.Equal(withDocFile, withoutDocFile);
        Assert.Contains(
            "Description: \"Indígena — descrição em várias linhas.\"",
            withoutDocFile,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A quadruple slash is explicitly not a documentation comment in C#, so the
    /// raw-trivia fallback must ignore it and let the chain fall through — otherwise the
    /// fallback would resolve descriptions the compiler itself never would.
    /// </summary>
    [Fact]
    public void QuadrupleSlashComment_IsNotTreatedAsDocumentation()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.XmlDoc;

            [DataDictionary("RacaCor")]
            public enum RacaCor
            {
                //// <summary>Indígena</summary>
                [DictionaryValue("I")]
                Indigena = 5,
            }
            """;

        var output = GeneratorTestHelper.Run(DocumentationMode.None, source);

        Assert.DoesNotContain("Description: \"Indígena\"", output, StringComparison.Ordinal);
        Assert.Contains("Description: \"Indigena\"", output, StringComparison.Ordinal);
    }
}
