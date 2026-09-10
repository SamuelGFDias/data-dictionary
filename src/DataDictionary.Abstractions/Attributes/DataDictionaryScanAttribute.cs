namespace DataDictionary.Abstractions;

/// <summary>
/// Assembly-level, repeatable declaration that every <see langword="enum"/> inside
/// the given namespace prefix is dictionary-eligible without requiring an explicit
/// <see cref="DataDictionaryAttribute"/>. A scanned enum's <c>EnumKey</c> defaults
/// to its simple name unless a <see cref="DataDictionaryAttribute"/> is still
/// present to override it — explicit attributes on a scanned enum are
/// additive/overriding, not conflicting. See
/// <c>contracts/attributes-contract.md</c>'s "Assembly-level convention mode"
/// example.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class DataDictionaryScanAttribute : Attribute
{
    /// <summary>Declares one namespace prefix to scan for dictionary-eligible enums.</summary>
    /// <param name="namespacePrefix">
    /// The namespace prefix to scan. Every enum declared inside this namespace (or
    /// a nested one) is treated as dictionary-eligible.
    /// </param>
    public DataDictionaryScanAttribute(string namespacePrefix)
    {
        NamespacePrefix = namespacePrefix ?? throw new ArgumentNullException(nameof(namespacePrefix));
    }

    /// <summary>The namespace prefix to scan for dictionary-eligible enums.</summary>
    public string NamespacePrefix { get; }
}
