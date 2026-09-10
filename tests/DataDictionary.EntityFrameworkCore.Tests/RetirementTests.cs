using DataDictionary.Abstractions.Configuration;
using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Persistence;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core;
using DataDictionary.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DataDictionary.EntityFrameworkCore.Tests;

/// <summary>
/// Integration coverage (T063) for <c>quickstart.md</c> Scenario D against real,
/// ephemeral SQL Server and PostgreSQL instances, end-to-end through
/// <see cref="DataDictionarySynchronizer"/>: starting from Scenario A's state (one
/// synced <c>RacaCor|Branca|B|1</c> row) with no business data ever referencing that
/// code, removing <c>Branca</c> from the manifest handed to the synchronizer and
/// running <see cref="SyncMode.Sync"/> must succeed (no exception), leaving the
/// <c>RacaCor</c>/<c>Branca</c> row present but with <c>IsActive == false</c> — retired,
/// never deleted.
/// </summary>
public sealed class RetirementTests
{
    private const string EnumKey = "RacaCor";
    private const string FieldName = "Branca";
    private const string Code = "B";
    private const long NumericValue = 1;
    private const string Description = "Branca";
    private const string GroupName = "Cadastro";

    /// <summary>
    /// The consumer <see cref="DbContext"/> under test — no business entity is mapped,
    /// so <see cref="EfDataDictionaryStore.IsCodeInUseAsync"/> finds nothing referencing
    /// the removed code's CLR enum type when it scans <see cref="DbContext.Model"/>.
    /// </summary>
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyDataDictionary();
        }
    }

    /// <summary>A minimal enum standing in for the sample's own <c>RacaCor</c>.</summary>
    private enum RacaCor
    {
        Branca = 1,
    }

    private static DataDictionaryManifest BuildFullManifest() => new(
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

    private static DataDictionaryManifest BuildReducedManifestMissingBranca() => new(
    [
        new ManifestEnumEntry(
            EnumKey: EnumKey,
            GroupName: GroupName,
            ClrFullName: typeof(RacaCor).FullName!,
            AssemblyName: typeof(RacaCor).Assembly.GetName().Name!,
            Description: Description,
            IsFlags: false,
            Members: []),
    ]);

    private static DictionaryEntry BuildBrancaEntry() => new()
    {
        EnumKey = EnumKey,
        FieldName = FieldName,
        Code = Code,
        NumericValue = NumericValue,
        Description = Description,
        GroupName = GroupName,
        IsActive = true,
        IsDeprecated = false,
        SortOrder = 0,
        ContentHash = "test-content-hash",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static async Task RunScenarioAsync(TestDbContext context)
    {
        var fullManifest = BuildFullManifest();

        // Scenario A's starting state: one synced dictionary row, applied insert-only
        // exactly as FreshSyncTests does.
        var seedStore = new EfDataDictionaryStore(context, new DataDictionaryOptions(Manifests: [fullManifest]));

        await seedStore.ApplyAsync(
            new SynchronizationOutcome(
                ToInsert: [BuildBrancaEntry()],
                ToUpdate: [],
                ToDeactivate: [],
                BreakingChanges: [],
                Unchanged: []),
            CancellationToken.None);

        // No business row ever referenced code 'B' — unlike BreakingChangeTests, this
        // context maps no entity of type RacaCor at all, so IsCodeInUseAsync's scan of
        // DbContext.Model finds nothing referencing it.
        var store = new EfDataDictionaryStore(context, new DataDictionaryOptions(Manifests: [fullManifest]));

        var reducedManifest = BuildReducedManifestMissingBranca();
        var options = new DataDictionaryOptions(
            Manifests: [reducedManifest],
            SyncMode: SyncMode.Sync,
            OnBreakingChange: OnBreakingChange.Fail);

        var synchronizer = new DataDictionarySynchronizer(
            options,
            store,
            new DictionaryDiffEngine(),
            new BreakingChangePolicyEngine());

        // Boot succeeds normally — no exception.
        await synchronizer.SynchronizeAsync(CancellationToken.None);

        // The row is retired, not removed: still present, now inactive.
        var afterState = await store.GetCurrentAsync(EnumKey, CancellationToken.None);
        var entry = Assert.Single(afterState.Entries);
        Assert.Equal(FieldName, entry.FieldName);
        Assert.Equal(Code, entry.Code);
        Assert.False(entry.IsActive);
    }

    public sealed class SqlServer : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task Sync_RemovedMemberCodeUnused_DeactivatesRow_BootSucceeds()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

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
        public async Task Sync_RemovedMemberCodeUnused_DeactivatesRow_BootSucceeds()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await RunScenarioAsync(context);
        }
    }
}
