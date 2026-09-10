using DataDictionary.Abstractions.Configuration;
using DataDictionary.Abstractions.Manifest;
using DataDictionary.Core;
using DataDictionary.Core.Sync;
using DataDictionary.EntityFrameworkCore.Seeding;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DataDictionary.EntityFrameworkCore.Tests;

/// <summary>
/// Integration coverage (T072) for the opt-in <c>SeedStrategy.Migration</c> path
/// (<see cref="MigrationSeedStrategy.SeedDataDictionary"/>, T071) against real, ephemeral SQL
/// Server and PostgreSQL instances: a row materialized purely via EF Core <c>HasData</c> —
/// never touched by <see cref="DataDictionary.Core.DataDictionarySynchronizer"/> — must be
/// indistinguishable, to <see cref="DictionaryDiffEngine"/>, from a row the runtime
/// synchronizer would have inserted itself. Diffing the same manifest used to seed the row
/// must classify it <c>Unchanged</c>, never <c>ToInsert</c> or <c>ToUpdate</c>.
/// </summary>
public sealed class MigrationSeedStrategyTests
{
    private const string EnumKey = "RacaCor";
    private const string FieldName = "Branca";
    private const string Code = "B";
    private const long NumericValue = 1;
    private const string Description = "Branca";
    private const string GroupName = "Cadastro";

    /// <summary>A minimal enum standing in for the sample's own <c>RacaCor</c>.</summary>
    private enum RacaCor
    {
        Branca = 1,
    }

    private static DataDictionaryManifest BuildManifest() => new(
    [
        new ManifestEnumEntry(
            EnumKey: EnumKey,
            GroupName: GroupName,
            ClrFullName: typeof(RacaCor).FullName!,
            AssemblyName: typeof(RacaCor).Assembly.GetName().Name!,
            Description: Description,
            IsFlags: false,
            Members:
            [
                new ManifestMemberEntry(
                    FieldName: FieldName,
                    Code: Code,
                    NumericValue: NumericValue,
                    Description: Description,
                    GroupName: GroupName,
                    IsDeprecated: false,
                    SortOrder: 0),
            ]),
    ]);

    /// <summary>
    /// The consumer <see cref="DbContext"/> under test. <c>OnModelCreating</c> calls
    /// <c>ApplyDataDictionary</c> (base entity mapping) followed by <c>SeedDataDictionary</c>
    /// (the opt-in <c>HasData</c> strategy under test) — the exact call order a real consumer
    /// opting into <c>SeedStrategy.Migration</c> would use.
    /// </summary>
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyDataDictionary();
            modelBuilder.SeedDataDictionary(BuildManifest());
        }
    }

    private static async Task RunScenarioAsync(TestDbContext context)
    {
        var manifest = BuildManifest();

        // EnsureCreatedAsync materializes the current model from scratch, including every
        // HasData row configured in OnModelCreating — no runtime synchronizer, no
        // DataDictionarySynchronizer.SynchronizeAsync call, involved at any point.
        await context.Database.EnsureCreatedAsync();

        var store = new EfDataDictionaryStore(context, new DataDictionaryOptions(Manifests: [manifest]));
        var currentState = await store.GetCurrentAsync(EnumKey, CancellationToken.None);

        var diffEngine = new DictionaryDiffEngine();
        var outcome = await diffEngine.DiffAsync(
            manifest.Enums[0],
            currentState,
            store,
            CancellationToken.None);

        Assert.Empty(outcome.ToInsert);
        Assert.Empty(outcome.ToUpdate);
        Assert.Empty(outcome.BreakingChanges);
        Assert.Empty(outcome.ToDeactivate);

        var unchangedEntry = Assert.Single(outcome.Unchanged);
        Assert.Equal(FieldName, unchangedEntry.FieldName);
        Assert.Equal(EnumKey, unchangedEntry.EnumKey);
        Assert.Equal(Code, unchangedEntry.Code);
        Assert.True(unchangedEntry.IsActive);
    }

    public sealed class SqlServer : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task HasDataSeededRow_DiffsAsUnchanged_AgainstSameManifest()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);

            await RunScenarioAsync(context);
        }
    }

    public sealed class PostgreSql : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container =
            new PostgreSqlBuilder("postgres:16-alpine").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task HasDataSeededRow_DiffsAsUnchanged_AgainstSameManifest()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);

            await RunScenarioAsync(context);
        }
    }
}
