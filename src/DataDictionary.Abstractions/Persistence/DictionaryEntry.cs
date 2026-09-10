namespace DataDictionary.Abstractions.Persistence;

/// <summary>
/// One persisted row of the data dictionary table (<c>tb_dicionario_dados</c> by
/// default naming — see <c>DataDictionaryNamingOptions</c>). Maps one member of one
/// marked enum. Never hard-deleted; a member removed from its source enum is
/// retired via <see cref="IsActive"/> instead (see the state-transition notes on
/// each property below and <c>data-model.md</c>'s <c>DictionaryEntry</c> section).
/// </summary>
public sealed class DictionaryEntry
{
    /// <summary>
    /// The owning enum's stable identifier. Part of the natural composite primary
    /// key <c>(enum_key, field_name)</c>.
    /// </summary>
    public string EnumKey { get; set; } = string.Empty;

    /// <summary>
    /// The enum member's C# name. Part of the natural composite primary key
    /// <c>(enum_key, field_name)</c>.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// The resolved dictionary code. Unique per <see cref="EnumKey"/> among rows
    /// where <see cref="IsActive"/> is <see langword="true"/> (filtered unique
    /// index).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The member's underlying numeric value, widened to <see cref="long"/>.</summary>
    public long NumericValue { get; set; }

    /// <summary>The member's resolved description, if any.</summary>
    public string? Description { get; set; }

    /// <summary>The member's group, if any.</summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// Whether this row is still current. Set to <see langword="false"/> only via
    /// the "removed and unused" retirement path (FR-017); a row is never
    /// hard-deleted, and nothing in this feature transitions a row back from
    /// <see langword="false"/> to <see langword="true"/> — the removed enum member
    /// no longer exists in the manifest to justify recreating it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether this member is deprecated.</summary>
    public bool IsDeprecated { get; set; }

    /// <summary>The member's sort position.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// A hash of the fields compared for "did this entry change" (description,
    /// group, sort order, deprecated flag). Used by the diff engine to
    /// short-circuit unchanged rows without touching <see cref="UpdatedAt"/>.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>The timestamp this row was first inserted. Set once, on insert.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The timestamp this row was last updated. Set only when an update actually
    /// changes a compared field; remains <see langword="null"/> for a row that has
    /// never been updated since insert.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
