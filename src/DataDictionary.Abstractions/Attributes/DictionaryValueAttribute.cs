namespace DataDictionary.Abstractions;

/// <summary>
/// Applied to a member of an <see langword="enum"/> decorated with
/// <see cref="DataDictionaryAttribute"/> to declare its explicit dictionary code.
/// See <c>contracts/attributes-contract.md</c> for the full explicit-mode example.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class DictionaryValueAttribute : Attribute
{
    /// <summary>Declares the explicit dictionary code for the decorated member.</summary>
    /// <param name="code">
    /// The resolved code for this member (source recorded as
    /// <see cref="CodeSource.Explicit"/>). Must be unique within the owning enum
    /// (diagnostic DD0002) and within the configured maximum code length
    /// (diagnostic DD0003).
    /// </param>
    public DictionaryValueAttribute(string code)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }

    /// <summary>The explicit dictionary code for the decorated member.</summary>
    public string Code { get; }

    /// <summary>
    /// Marks this member as deprecated in the dictionary without removing it from
    /// the enum or the persisted dictionary row. Defaults to <see langword="false"/>.
    /// </summary>
    public bool Deprecated { get; set; }
}
