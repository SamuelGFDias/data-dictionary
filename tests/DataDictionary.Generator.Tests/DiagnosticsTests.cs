using VerifyXunit;
using Xunit;

namespace DataDictionary.Generator.Tests;

/// <summary>
/// One positive (fires) and one negative (does not fire, on otherwise-valid input) case
/// per diagnostic DD0001-DD0007 (T034), per
/// <c>contracts/diagnostics-contract.md</c>. DD0008 is explicitly out of scope for this
/// feature (reserved, not implemented) and has no test here.
/// </summary>
public class DiagnosticsTests
{
    // ---- DD0001: member code cannot be resolved -------------------------------------

    [Fact]
    public Task DD0001_Fires_WhenExplicitModeMemberHasNoDictionaryValue()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0001;

            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("A")]
                A,
                B,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task DD0001_DoesNotFire_WhenEveryMemberHasDictionaryValue()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0001;

            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("A")]
                A,
                [DictionaryValue("B")]
                B,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    // ---- DD0002: duplicate code within the same enum ---------------------------------

    [Fact]
    public Task DD0002_Fires_WhenTwoMembersResolveToTheSameCode()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0002;

            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("A")]
                A,
                [DictionaryValue("A")]
                B,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task DD0002_DoesNotFire_WhenCodesAreDistinct()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0002;

            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("A")]
                A,
                [DictionaryValue("B")]
                B,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    // ---- DD0003: code exceeds the configured maximum length --------------------------

    [Fact]
    public Task DD0003_Fires_WhenCodeExceedsMaxLength()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0003;

            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("ThisCodeIsDeliberatelyLongerThanTheSixtyFourCharacterLimitAllowed")]
                A,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task DD0003_DoesNotFire_WhenCodeIsWithinMaxLength()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0003;

            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("A")]
                A,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    // ---- DD0004: no resolvable description with RequireDescription = true -----------

    [Fact]
    public Task DD0004_Fires_WhenRequireDescriptionTrueAndNothingResolves()
    {
        const string source = """
            [assembly: DataDictionary.Abstractions.DataDictionaryDefaults(RequireDescription = true)]
            [assembly: DataDictionary.Abstractions.DataDictionaryScan("Fixture.Diagnostics.DD0004")]

            namespace Fixture.Diagnostics.DD0004;

            public enum Foo
            {
                A,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task DD0004_DoesNotFire_WhenXmlDocSummaryResolves()
    {
        const string source = """
            [assembly: DataDictionary.Abstractions.DataDictionaryDefaults(RequireDescription = true)]
            [assembly: DataDictionary.Abstractions.DataDictionaryScan("Fixture.Diagnostics.DD0004Negative")]

            namespace Fixture.Diagnostics.DD0004Negative;

            public enum Foo
            {
                /// <summary>Descrição real do valor A.</summary>
                A,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    // ---- DD0005: empty or duplicate enum key ------------------------------------------

    [Fact]
    public Task DD0005_Fires_WhenTwoEnumsShareTheSameKey()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0005;

            [DataDictionary("Same")]
            public enum FirstEnum
            {
                [DictionaryValue("A")]
                A,
            }

            [DataDictionary("Same")]
            public enum SecondEnum
            {
                [DictionaryValue("B")]
                B,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task DD0005_DoesNotFire_WhenEnumKeysAreDistinct()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0005;

            [DataDictionary("First")]
            public enum FirstEnum
            {
                [DictionaryValue("A")]
                A,
            }

            [DataDictionary("Second")]
            public enum SecondEnum
            {
                [DictionaryValue("B")]
                B,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    // ---- DD0006: numeric alias within the same enum -----------------------------------

    [Fact]
    public Task DD0006_Fires_WhenTwoMembersShareTheSameNumericValue()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0006;

            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("A")]
                A = 1,
                [DictionaryValue("B")]
                B = 1,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task DD0006_DoesNotFire_WhenNumericValuesAreDistinct()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0006;

            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("A")]
                A = 1,
                [DictionaryValue("B")]
                B = 2,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    // ---- DD0007: [Flags] enum marked as a dictionary source ---------------------------

    [Fact]
    public Task DD0007_Fires_WhenFlagsEnumIsMarked()
    {
        const string source = """
            using System;
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0007;

            [Flags]
            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("A")]
                A = 1,
                [DictionaryValue("B")]
                B = 2,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }

    [Fact]
    public Task DD0007_DoesNotFire_WhenEnumIsNotFlags()
    {
        const string source = """
            using DataDictionary.Abstractions;

            namespace Fixture.Diagnostics.DD0007;

            [DataDictionary("Foo")]
            public enum Foo
            {
                [DictionaryValue("A")]
                A = 1,
                [DictionaryValue("B")]
                B = 2,
            }
            """;

        return Verifier.Verify(GeneratorTestHelper.Run(source));
    }
}
