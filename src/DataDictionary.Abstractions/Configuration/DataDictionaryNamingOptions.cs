namespace DataDictionary.Abstractions.Configuration;

/// <summary>
/// Naming-convention configuration for the tables and columns the library maps
/// (FR-011). Every default here matches <c>spec.md</c> <c>## Clarifications</c>:
/// snake_case columns, matching every column name already used throughout the
/// specification (e.g. <c>enum_key</c>, <c>field_name</c>, <c>code</c>), with
/// default table names <c>tb_dicionario_dados</c> and <c>tb_dicionario_enum</c> in
/// the database's default schema.
/// </summary>
public sealed class DataDictionaryNamingOptions
{
    /// <summary>
    /// The table name for the dictionary entries table. Defaults to
    /// <c>tb_dicionario_dados</c>.
    /// </summary>
    public string DictionaryTableName { get; set; } = "tb_dicionario_dados";

    /// <summary>
    /// The table name for the optional enum catalog table. Defaults to
    /// <c>tb_dicionario_enum</c>.
    /// </summary>
    public string EnumCatalogTableName { get; set; } = "tb_dicionario_enum";

    /// <summary>
    /// The database schema both tables are mapped into. <see langword="null"/>
    /// (the default) uses the database's default schema.
    /// </summary>
    public string? Schema { get; set; }

    /// <summary>
    /// The convention used to derive column names. Defaults to
    /// <see cref="DataDictionaryColumnNamingConvention.SnakeCase"/>.
    /// </summary>
    public DataDictionaryColumnNamingConvention ColumnNamingConvention { get; set; } =
        DataDictionaryColumnNamingConvention.SnakeCase;
}

/// <summary>
/// The convention used to derive database column names from the public model's
/// property names.
/// </summary>
public enum DataDictionaryColumnNamingConvention
{
    /// <summary>
    /// <c>snake_case</c> — e.g. <c>EnumKey</c> maps to <c>enum_key</c>. The only
    /// convention exercised by this specification's examples; a natural extension
    /// point for future conventions.
    /// </summary>
    SnakeCase = 0,
}
