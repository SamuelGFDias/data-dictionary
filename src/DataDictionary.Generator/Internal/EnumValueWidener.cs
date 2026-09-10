namespace DataDictionary.Generator.Internal;

/// <summary>
/// Widens an enum member's underlying constant value (whatever its backing integral
/// type — <see cref="sbyte"/> through <see cref="ulong"/>) to <see cref="long"/>, per
/// <c>data-model.md</c>'s <c>DictionaryMemberModel.NumericValue</c> ("widened to
/// <c>long</c> to cover every enum backing type").
/// </summary>
internal static class EnumValueWidener
{
    /// <summary>
    /// Widens <paramref name="constantValue"/> (the boxed value from
    /// <c>IFieldSymbol.ConstantValue</c> for an enum member) to <see cref="long"/>.
    /// </summary>
    internal static long ToInt64(object? constantValue) => constantValue switch
    {
        sbyte v => v,
        byte v => v,
        short v => v,
        ushort v => v,
        int v => v,
        uint v => v,
        long v => v,
        // ulong values above long.MaxValue lose their exact value here (an accepted,
        // documented limitation of widening every backing type to a single signed
        // 64-bit field) rather than throwing and aborting the whole generator run.
        ulong v => unchecked((long)v),
        _ => 0,
    };
}
