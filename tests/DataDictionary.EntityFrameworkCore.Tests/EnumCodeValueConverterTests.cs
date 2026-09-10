using Xunit;

namespace DataDictionary.EntityFrameworkCore.Tests;

/// <summary>
/// Unit coverage for <see cref="EnumCodeValueConverter{TEnum}"/>'s <c>ToCode</c>/<c>FromCode</c>
/// conversion behavior in isolation, without a <see cref="Microsoft.EntityFrameworkCore.ModelBuilder"/>
/// or a real database — pure in-memory logic. Covers the O(1) lookup rewrite of <c>FromCode</c>:
/// round-trip correctness for every enum member, the error path for an unregistered code (same
/// exception type and message as the previous linear-scan implementation), and that the lookup
/// does not depend on member position/order in the source dictionary.
/// </summary>
public sealed class EnumCodeValueConverterTests
{
    public enum SmallFixtureEnum
    {
        Alpha,
        Beta,
        Gamma,
    }

    private static readonly IReadOnlyDictionary<SmallFixtureEnum, string> SmallCodeByValue =
        new Dictionary<SmallFixtureEnum, string>
        {
            [SmallFixtureEnum.Alpha] = "A",
            [SmallFixtureEnum.Beta] = "B",
            [SmallFixtureEnum.Gamma] = "G",
        };

    [Theory]
    [InlineData(SmallFixtureEnum.Alpha, "A")]
    [InlineData(SmallFixtureEnum.Beta, "B")]
    [InlineData(SmallFixtureEnum.Gamma, "G")]
    public void ToCode_then_FromCode_round_trips_every_member(SmallFixtureEnum value, string expectedCode)
    {
        var converter = new EnumCodeValueConverter<SmallFixtureEnum>(SmallCodeByValue);

        var code = converter.ConvertToProvider(value);
        Assert.Equal(expectedCode, code);

        var roundTripped = converter.ConvertFromProvider(code);
        Assert.Equal(value, roundTripped);
    }

    [Fact]
    public void FromCode_with_unregistered_code_throws_expected_message()
    {
        var converter = new EnumCodeValueConverter<SmallFixtureEnum>(SmallCodeByValue);

        var exception = Assert.Throws<InvalidOperationException>(
            () => converter.ConvertFromProvider("Z"));

        Assert.Equal(
            $"No member of '{typeof(SmallFixtureEnum).FullName}' is registered for dictionary code 'Z'.",
            exception.Message);
    }

    private enum LargeFixtureEnum
    {
        Member00,
        Member01,
        Member02,
        Member03,
        Member04,
        Member05,
        Member06,
        Member07,
        Member08,
        Member09,
        Member10,
        Member11,
        Member12,
        Member13,
        Member14,
        Member15,
        Member16,
        Member17,
        Member18,
        Member19,
        Member20,
        Member21,
        Member22,
        Member23,
    }

    /// <summary>
    /// Codes are assigned in reverse member order, so a correct round trip for every
    /// member cannot be explained by coincidentally matching dictionary insertion order
    /// or enum declaration position — it demonstrates the O(1) lookup is keyed purely by
    /// code value.
    /// </summary>
    private static IReadOnlyDictionary<LargeFixtureEnum, string> BuildLargeCodeByValue()
    {
        var values = Enum.GetValues<LargeFixtureEnum>();
        var codeByValue = new Dictionary<LargeFixtureEnum, string>();

        for (var i = 0; i < values.Length; i++)
        {
            var code = $"C{values.Length - 1 - i:D2}";
            codeByValue[values[i]] = code;
        }

        return codeByValue;
    }

    [Fact]
    public void Round_trips_every_member_of_a_large_enum_regardless_of_order()
    {
        var codeByValue = BuildLargeCodeByValue();
        var converter = new EnumCodeValueConverter<LargeFixtureEnum>(codeByValue);

        foreach (var (value, expectedCode) in codeByValue)
        {
            var code = converter.ConvertToProvider(value);
            Assert.Equal(expectedCode, code);

            var roundTripped = converter.ConvertFromProvider(code);
            Assert.Equal(value, roundTripped);
        }
    }
}
