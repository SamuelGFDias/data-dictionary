using System.Text;
using DataDictionary.Abstractions.Configuration;
using DataDictionary.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataDictionary.EntityFrameworkCore;

/// <summary>
/// <see cref="ModelBuilder"/> extension wiring the data dictionary's own tables —
/// <c>tb_dicionario_dados</c> (<see cref="DictionaryEntry"/>) and, optionally,
/// <c>tb_dicionario_enum</c> (<see cref="DictionaryEnumCatalogEntry"/>) — into a
/// consumer's EF Core model. This is the base entity mapping only (naming, keys, the
/// filtered unique index); wiring the generated per-property enum
/// <c>ValueConverter</c>s onto business entities is
/// <see cref="EnumCodeValueConverterExtensions.ApplyEnumCodeConverters"/>. See
/// <c>contracts/generated-entrypoints-contract.md</c> and <c>data-model.md</c>.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Configures the data dictionary's entity mappings on <paramref name="modelBuilder"/>:
    /// default (or configured) table names, snake_case (or configured) column names, the
    /// natural composite primary keys, and the filtered unique index on
    /// <c>(enum_key, code)</c> restricted to active rows (<c>is_active = 1</c>).
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure, normally the one passed
    /// into the consumer's <c>DbContext.OnModelCreating</c>.</param>
    /// <param name="namingOptions">The naming convention to apply (table names, schema,
    /// column naming convention). Defaults to <see cref="DataDictionaryNamingOptions"/>'s
    /// own defaults (FR-011) when omitted.</param>
    /// <returns><paramref name="modelBuilder"/>, for chaining.</returns>
    public static ModelBuilder ApplyDataDictionary(
        this ModelBuilder modelBuilder,
        DataDictionaryNamingOptions? namingOptions = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        namingOptions ??= new DataDictionaryNamingOptions();

        ConfigureDictionaryEntry(modelBuilder, namingOptions);
        ConfigureDictionaryEnumCatalogEntry(modelBuilder, namingOptions);

        return modelBuilder;
    }

    private static void ConfigureDictionaryEntry(
        ModelBuilder modelBuilder,
        DataDictionaryNamingOptions namingOptions)
    {
        modelBuilder.Entity<DictionaryEntry>(entity =>
        {
            entity.ToTable(namingOptions.DictionaryTableName, namingOptions.Schema);

            // Natural composite primary key (enum_key, field_name) — see data-model.md.
            entity.HasKey(e => new { e.EnumKey, e.FieldName });

            entity.Property(e => e.EnumKey)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.EnumKey), namingOptions))
                .IsRequired();

            entity.Property(e => e.FieldName)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.FieldName), namingOptions))
                .IsRequired();

            entity.Property(e => e.Code)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.Code), namingOptions))
                .IsRequired();

            entity.Property(e => e.NumericValue)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.NumericValue), namingOptions));

            entity.Property(e => e.Description)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.Description), namingOptions));

            entity.Property(e => e.GroupName)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.GroupName), namingOptions));

            // Stored as int (0/1), not the provider's native boolean type, so the
            // filtered unique index below can use the single, ANSI-portable predicate
            // "<is_active column> = 1" on both SQL Server and PostgreSQL — a native
            // `boolean` column on PostgreSQL rejects `= 1` (integer) outright, and
            // ModelBuilder has no supported way to detect the active provider at this
            // point to emit provider-specific filter text instead.
            var isActiveColumnName = ColumnName(nameof(DictionaryEntry.IsActive), namingOptions);
            entity.Property(e => e.IsActive)
                .HasColumnName(isActiveColumnName)
                .HasConversion<int>();

            entity.Property(e => e.IsDeprecated)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.IsDeprecated), namingOptions));

            entity.Property(e => e.SortOrder)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.SortOrder), namingOptions));

            entity.Property(e => e.ContentHash)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.ContentHash), namingOptions))
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.CreatedAt), namingOptions));

            entity.Property(e => e.UpdatedAt)
                .HasColumnName(ColumnName(nameof(DictionaryEntry.UpdatedAt), namingOptions));

            // Unique per enum_key among active rows only (data-model.md `DictionaryEntry`).
            entity.HasIndex(e => new { e.EnumKey, e.Code })
                .IsUnique()
                .HasDatabaseName($"ix_{namingOptions.DictionaryTableName}_enum_key_code_active")
                .HasFilter($"{isActiveColumnName} = 1");
        });
    }

    private static void ConfigureDictionaryEnumCatalogEntry(
        ModelBuilder modelBuilder,
        DataDictionaryNamingOptions namingOptions)
    {
        modelBuilder.Entity<DictionaryEnumCatalogEntry>(entity =>
        {
            entity.ToTable(namingOptions.EnumCatalogTableName, namingOptions.Schema);

            entity.HasKey(e => e.EnumKey);

            entity.Property(e => e.EnumKey)
                .HasColumnName(ColumnName(nameof(DictionaryEnumCatalogEntry.EnumKey), namingOptions))
                .IsRequired();

            entity.Property(e => e.ClrFullName)
                .HasColumnName(ColumnName(nameof(DictionaryEnumCatalogEntry.ClrFullName), namingOptions))
                .IsRequired();

            entity.Property(e => e.AssemblyName)
                .HasColumnName(ColumnName(nameof(DictionaryEnumCatalogEntry.AssemblyName), namingOptions))
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName(ColumnName(nameof(DictionaryEnumCatalogEntry.Description), namingOptions));

            entity.Property(e => e.GroupName)
                .HasColumnName(ColumnName(nameof(DictionaryEnumCatalogEntry.GroupName), namingOptions));

            entity.Property(e => e.ManifestHash)
                .HasColumnName(ColumnName(nameof(DictionaryEnumCatalogEntry.ManifestHash), namingOptions))
                .IsRequired();

            entity.Property(e => e.LastSyncAt)
                .HasColumnName(ColumnName(nameof(DictionaryEnumCatalogEntry.LastSyncAt), namingOptions));
        });
    }

    /// <summary>
    /// Resolves the database column name for <paramref name="propertyName"/> under
    /// <paramref name="namingOptions"/>'s configured
    /// <see cref="DataDictionaryColumnNamingConvention"/>.
    /// </summary>
    private static string ColumnName(string propertyName, DataDictionaryNamingOptions namingOptions) =>
        namingOptions.ColumnNamingConvention switch
        {
            DataDictionaryColumnNamingConvention.SnakeCase => ToSnakeCase(propertyName),
            _ => ToSnakeCase(propertyName),
        };

    /// <summary>
    /// Converts a PascalCase CLR property name (e.g. <c>EnumKey</c>) to snake_case (e.g.
    /// <c>enum_key</c>). Every property mapped by this class uses plain PascalCase words
    /// with no embedded acronyms, so a straightforward "underscore before each interior
    /// uppercase letter" pass is sufficient.
    /// </summary>
    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
