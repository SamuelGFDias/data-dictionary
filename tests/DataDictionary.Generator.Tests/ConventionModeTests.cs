using VerifyXunit;
using Xunit;

namespace DataDictionary.Generator.Tests;

/// <summary>
/// Convention mode (T026): enums reached purely through
/// <c>[assembly: DataDictionaryScan]</c>, with and without
/// <c>[assembly: DataDictionaryDefaults]</c>, including the
/// <c>RequireDescription = true</c> case.
/// </summary>
public class ConventionModeTests
{
    [Fact]
    public Task ScannedEnum_NoExplicitDefaults_UsesMemberNameCodeAndXmlDocDescription()
    {
        const string source = """
            [assembly: DataDictionary.Abstractions.DataDictionaryScan("Fixture.Convention.Plain")]

            namespace Fixture.Convention.Plain;

            /// <summary>Situação cadastral.</summary>
            public enum Situacao
            {
                /// <summary>Cadastro ativo.</summary>
                Ativo,

                /// <summary>Cadastro inativo.</summary>
                Inativo,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task ScannedEnum_WithDefaults_RequireDescriptionFalse_FallsBackToMemberNameDescription()
    {
        const string source = """
            [assembly: DataDictionary.Abstractions.DataDictionaryDefaults(RequireDescription = false)]
            [assembly: DataDictionary.Abstractions.DataDictionaryScan("Fixture.Convention.NotRequired")]

            namespace Fixture.Convention.NotRequired;

            public enum Situacao
            {
                Ativo,
                Inativo,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task ScannedEnum_WithDefaults_RequireDescriptionTrue_WarnsWhenNoDescriptionResolves()
    {
        const string source = """
            [assembly: DataDictionary.Abstractions.DataDictionaryDefaults(RequireDescription = true)]
            [assembly: DataDictionary.Abstractions.DataDictionaryScan("Fixture.Convention.Required")]

            namespace Fixture.Convention.Required;

            public enum Situacao
            {
                Ativo,

                /// <summary>Cadastro inativo.</summary>
                Inativo,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task ScannedEnum_ExplicitDataDictionaryAttributeOverridesEnumKey()
    {
        const string source = """
            using DataDictionary.Abstractions;

            [assembly: DataDictionaryScan("Fixture.Convention.Override")]

            namespace Fixture.Convention.Override;

            [DataDictionary("SituacaoCadastral")]
            public enum Situacao
            {
                /// <summary>Ativo.</summary>
                Ativo,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }
}
