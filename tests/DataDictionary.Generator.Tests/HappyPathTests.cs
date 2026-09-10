using VerifyXunit;
using Xunit;

namespace DataDictionary.Generator.Tests;

/// <summary>
/// Explicit-mode happy path (T025): a single <c>[DataDictionary]</c>-marked enum with a
/// single <c>[DictionaryValue]</c>-marked member, once per description source in the
/// FR-005 precedence chain.
/// </summary>
public class HappyPathTests
{
    [Fact]
    public Task SingleEnum_SingleMember_DescriptionFromXmlDoc()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.HappyPath;

            /// <summary>Raça/cor declarada pelo cidadão.</summary>
            [DataDictionary("RacaCor")]
            public enum RacaCor
            {
                /// <summary>Cor branca.</summary>
                [DictionaryValue("B")]
                Branca,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task SingleEnum_SingleMember_DescriptionFromDescriptionAttribute()
    {
        const string source = """
            using System.ComponentModel;
            using DataDictionary.Abstractions;

            namespace Fixture.HappyPath;

            [DataDictionary("RacaCor")]
            public enum RacaCor
            {
                [DictionaryValue("B")]
                [Description("Cor branca")]
                Branca,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task SingleEnum_SingleMember_DescriptionFromDisplayAttribute()
    {
        const string source = """
            using System.ComponentModel.DataAnnotations;
            using DataDictionary.Abstractions;

            namespace Fixture.HappyPath;

            [DataDictionary("RacaCor")]
            public enum RacaCor
            {
                [DictionaryValue("B")]
                [Display(Name = "Cor branca")]
                Branca,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task SingleEnum_SingleMember_DescriptionFromMemberNameFallback()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.HappyPath;

            [DataDictionary("RacaCor")]
            public enum RacaCor
            {
                [DictionaryValue("B")]
                Branca,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }
}
