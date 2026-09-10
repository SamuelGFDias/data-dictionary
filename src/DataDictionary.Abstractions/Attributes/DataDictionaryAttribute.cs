namespace DataDictionary.Abstractions;

/// <summary>
/// Marks an <see langword="enum"/> declaration as a data dictionary source: its
/// members are synchronized into the persisted dictionary table
/// (<c>tb_dicionario_dados</c>) whenever the configured <see cref="Configuration.SyncMode"/> is
/// active. See <c>contracts/attributes-contract.md</c> for the full explicit-mode
/// example.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class DataDictionaryAttribute : Attribute
{
    /// <summary>
    /// Marks the decorated <see langword="enum"/> as a data dictionary source.
    /// </summary>
    /// <param name="enumKey">
    /// The stable identifier for this enum in the persisted dictionary. Must be
    /// non-empty and unique across the whole compilation (diagnostic DD0005
    /// otherwise). Defaults to the enum's simple name when the enum is instead
    /// picked up via <see cref="DataDictionaryScanAttribute"/> convention scanning.
    /// </param>
    public DataDictionaryAttribute(string enumKey)
    {
        EnumKey = enumKey ?? throw new ArgumentNullException(nameof(enumKey));
    }

    /// <summary>The stable identifier for this enum in the persisted dictionary.</summary>
    public string EnumKey { get; }

    /// <summary>
    /// Optional grouping label applied to the enum and, by default, inherited by
    /// every member of it that does not resolve its own group.
    /// </summary>
    public string? Group { get; set; }
}
