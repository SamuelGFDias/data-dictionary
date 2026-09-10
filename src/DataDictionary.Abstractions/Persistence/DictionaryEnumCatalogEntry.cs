namespace DataDictionary.Abstractions.Persistence;

/// <summary>
/// One persisted row of the optional enum catalog table (<c>tb_dicionario_enum</c>
/// by default naming — see <c>DataDictionaryNamingOptions</c>). Maps one marked
/// enum. <see cref="EnumKey"/> is the join key back to every
/// <see cref="DictionaryEntry"/> row for the same enum.
/// </summary>
public sealed class DictionaryEnumCatalogEntry
{
    /// <summary>The enum's stable identifier. Primary key.</summary>
    public string EnumKey { get; set; } = string.Empty;

    /// <summary>The enum's fully-qualified CLR type name.</summary>
    public string ClrFullName { get; set; } = string.Empty;

    /// <summary>The declaring assembly's simple name.</summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>The enum's resolved description, if any.</summary>
    public string? Description { get; set; }

    /// <summary>The enum's group, if any.</summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// A hash of the whole enum's member set, used to short-circuit a no-op sync
    /// pass for enums that did not change at all.
    /// </summary>
    public string ManifestHash { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp this enum last participated in a synchronization pass,
    /// whether or not any member row changed.
    /// </summary>
    public DateTimeOffset LastSyncAt { get; set; }
}
