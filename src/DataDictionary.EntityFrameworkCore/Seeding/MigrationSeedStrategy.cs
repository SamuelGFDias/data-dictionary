using DataDictionary.Abstractions.Configuration;
using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Persistence;
using DataDictionary.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace DataDictionary.EntityFrameworkCore.Seeding;

/// <summary>
/// <see cref="ModelBuilder"/> extension implementing the opt-in <c>SeedStrategy.Migration</c>
/// path (<c>research.md</c> §8): bakes every manifest member into <c>tb_dicionario_dados</c>
/// via EF Core's <c>HasData</c>, so the rows ship as part of the generated migration instead
/// of (or in addition to) being written by <see cref="DataDictionarySynchronizer"/>
/// at startup.
/// </summary>
/// <remarks>
/// Runtime seeding remains the default and is required regardless of whether this strategy is
/// also used — it is what makes fail-fast divergence detection possible at all
/// (<c>research.md</c> §8). This extension only adds a second, opt-in way to get the initial
/// rows into the database; it does not replace or disable the runtime synchronizer, and a
/// consumer must call it explicitly, after <see cref="ModelBuilderExtensions.ApplyDataDictionary"/>,
/// from <c>OnModelCreating</c>.
/// <para>
/// <c>HasData</c> is viable here specifically because <see cref="DictionaryEntry"/>'s primary
/// key is the natural, deterministic composite <c>(enum_key, field_name)</c> — EF Core's
/// snapshot-diffing model needs a stable key it can compare across migrations, and the natural
/// key satisfies that without introducing a surrogate key solely for this purpose.
/// </para>
/// <para>
/// This method does not resolve column names itself: <c>HasData</c> operates on the CLR
/// entity's properties, not on physical column names, so the column mapping already configured
/// by a preceding <see cref="ModelBuilderExtensions.ApplyDataDictionary"/> call is picked up
/// automatically.
/// </para>
/// </remarks>
public static class MigrationSeedStrategy
{
    /// <summary>
    /// The fixed <c>CreatedAt</c> stamped on every row seeded through this strategy.
    /// </summary>
    /// <remarks>
    /// <c>HasData</c> values are compared, unchanged property by unchanged property, against
    /// the previous migration's snapshot to compute the next migration's diff. A real "now"
    /// timestamp (e.g. <see cref="DateTimeOffset.UtcNow"/>) would therefore differ on every
    /// build and produce a spurious new migration — and a diverging value across environments
    /// — even when nothing about the dictionary changed. A fixed constant keeps the seeded data
    /// byte-for-byte identical between builds, which is what the snapshot diff needs to stay
    /// quiet. The actual "when was this enum member introduced" question is answered by source
    /// control history on the enum, not by this column, for migration-seeded rows.
    /// </remarks>
    public static readonly DateTimeOffset SeedCreatedAt = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Adds <c>HasData</c> seed rows for every member of every enum in <paramref name="manifest"/>
    /// to <see cref="DictionaryEntry"/>'s entity configuration on <paramref name="modelBuilder"/>.
    /// </summary>
    /// <param name="modelBuilder">
    /// The model builder to configure. Must already have had
    /// <see cref="ModelBuilderExtensions.ApplyDataDictionary"/> applied to it (normally earlier
    /// in the same <c>OnModelCreating</c>), so <see cref="DictionaryEntry"/> is mapped before
    /// this method attaches seed data to it.
    /// </param>
    /// <param name="manifest">The compile-time manifest to seed from — the same manifest passed
    /// to the runtime synchronizer, so a consumer using both strategies keeps a single source of
    /// truth for what "the dictionary" contains.</param>
    /// <param name="namingOptions">
    /// Unused by this method — accepted only to mirror <see cref="ModelBuilderExtensions.ApplyDataDictionary"/>'s
    /// signature for a consistent call site. <c>HasData</c> targets CLR properties, not column
    /// names, so no naming resolution is needed here; the physical column mapping already
    /// applied by <c>ApplyDataDictionary</c> covers it.
    /// </param>
    /// <returns><paramref name="modelBuilder"/>, for chaining.</returns>
    public static ModelBuilder SeedDataDictionary(
        this ModelBuilder modelBuilder,
        DataDictionaryManifest manifest,
        DataDictionaryNamingOptions? namingOptions = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(manifest);

        modelBuilder.Entity<DictionaryEntry>(entity =>
        {
            foreach (var enumEntry in manifest.Enums)
            {
                foreach (var member in enumEntry.Members)
                {
                    entity.HasData(new DictionaryEntry
                    {
                        EnumKey = enumEntry.EnumKey,
                        FieldName = member.FieldName,
                        Code = member.Code,
                        NumericValue = member.NumericValue,
                        Description = member.Description,
                        GroupName = member.GroupName,
                        IsDeprecated = member.IsDeprecated,
                        SortOrder = member.SortOrder,

                        // Every migration-seeded row starts active; retiring a row that has
                        // since been removed from the enum is the runtime synchronizer's job
                        // (FR-017/FR-018), not this strategy's — this method only ever adds
                        // rows for members currently in the manifest.
                        IsActive = true,

                        // Computed with the same hasher and the same four fields the diff
                        // engine hashes (DictionaryDiffEngine.DiffAsync), so a row seeded here
                        // is byte-for-byte indistinguishable from one the runtime synchronizer
                        // would have inserted — the diff engine classifies it Unchanged on the
                        // very first sync instead of ToUpdate.
                        ContentHash = DictionaryEntryHasher.Compute(
                            member.Description,
                            member.GroupName,
                            member.SortOrder,
                            member.IsDeprecated),

                        CreatedAt = SeedCreatedAt,
                        UpdatedAt = null,
                    });
                }
            }
        });

        return modelBuilder;
    }
}
