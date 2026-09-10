using DataDictionary.Abstractions.Configuration;
using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core;
using DataDictionary.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DataDictionary.EntityFrameworkCore.Tests;

/// <summary>
/// Integration coverage (T058) for <c>quickstart.md</c> Scenario B, plus additional
/// coverage (T061) for the <c>ToUpdate</c> persistence path, against real, ephemeral
/// SQL Server and PostgreSQL instances, end-to-end through
/// <see cref="DataDictionarySynchronizer"/> (not just <see cref="EfDataDictionaryStore"/>
/// in isolation) — the synchronizer is run twice over the same
/// <see cref="DbContext"/>/<see cref="EfDataDictionaryStore"/>, exactly as a real
/// process boot would reuse a scoped <see cref="DbContext"/> across
/// <see cref="EfDataDictionaryStore.GetCurrentAsync"/> and
/// <see cref="EfDataDictionaryStore.ApplyAsync"/>. This is also what exercises the
/// tracking bug that <c>AsNoTracking</c> in <see cref="EfDataDictionaryStore.GetCurrentAsync"/>
/// fixes: without it, the diff engine's brand-new <see cref="DictionaryEntry"/> instances
/// for <c>ToUpdate</c> collide with the still-tracked instances read back by the
/// preceding <c>GetCurrentAsync</c> call on the same context.
/// </summary>
public sealed class IdempotentSyncTests
{
    private const string EnumKey = "RacaCor";

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyDataDictionary();
        }
    }

    private static ManifestMemberEntry BuildBrancaMember() => new(
        FieldName: "Branca",
        Code: "B",
        NumericValue: 1,
        Description: "Branca",
        GroupName: "Cadastro",
        IsDeprecated: false,
        SortOrder: 0);

    private static ManifestMemberEntry BuildPretaMember(string description = "Cor preta") => new(
        FieldName: "Preta",
        Code: "P",
        NumericValue: 2,
        Description: description,
        GroupName: null,
        IsDeprecated: false,
        SortOrder: 0);

    private static DataDictionaryManifest BuildManifest(params ManifestMemberEntry[] members) => new(
    [
        new ManifestEnumEntry(
            EnumKey: EnumKey,
            GroupName: "Cadastro",
            ClrFullName: "DataDictionary.EntityFrameworkCore.Tests.IdempotentSyncTests+RacaCor",
            AssemblyName: typeof(IdempotentSyncTests).Assembly.GetName().Name!,
            Description: EnumKey,
            IsFlags: false,
            Members: [.. members]),
    ]);

    private static DataDictionarySynchronizer BuildSynchronizer(
        EfDataDictionaryStore store,
        DataDictionaryManifest manifest) =>
        new(
            new DataDictionaryOptions(
                Manifests: [manifest],
                SyncMode: SyncMode.Sync,
                OnBreakingChange: OnBreakingChange.Fail),
            store,
            new DictionaryDiffEngine(),
            new BreakingChangePolicyEngine());

    /// <summary>
    /// Scenario B (T058): starting from Scenario A's state (only <c>Branca</c> synced),
    /// a restart with a manifest that adds <c>Preta</c> must insert exactly one new row
    /// and must not touch the pre-existing, unchanged <c>Branca</c> row at all — same
    /// <c>CreatedAt</c>, <c>UpdatedAt</c> still <see langword="null"/>.
    /// </summary>
    private static async Task RunScenarioBAsync(DbContext context)
    {
        var store = new EfDataDictionaryStore(context, new DataDictionaryOptions(Manifests: []));

        // First pass: manifest with only Branca.
        var firstManifest = BuildManifest(BuildBrancaMember());
        var firstSynchronizer = BuildSynchronizer(store, firstManifest);
        await firstSynchronizer.SynchronizeAsync(CancellationToken.None);

        var afterFirstPass = await store.GetCurrentAsync(EnumKey, CancellationToken.None);
        var brancaAfterFirstPass = Assert.Single(afterFirstPass.Entries);
        Assert.Equal("Branca", brancaAfterFirstPass.FieldName);
        Assert.Null(brancaAfterFirstPass.UpdatedAt);

        // Second pass ("restart"): Branca unchanged, Preta is a brand-new member. Same
        // DbContext/store — a new synchronizer instance, as a real reboot would build.
        var secondManifest = BuildManifest(BuildBrancaMember(), BuildPretaMember());
        var secondSynchronizer = BuildSynchronizer(store, secondManifest);
        await secondSynchronizer.SynchronizeAsync(CancellationToken.None);

        var afterSecondPass = await store.GetCurrentAsync(EnumKey, CancellationToken.None);
        Assert.Equal(2, afterSecondPass.Entries.Count);

        var branca = Assert.Single(afterSecondPass.Entries, e => e.FieldName == "Branca");
        Assert.Equal(brancaAfterFirstPass.CreatedAt, branca.CreatedAt);
        Assert.Null(branca.UpdatedAt);

        var preta = Assert.Single(afterSecondPass.Entries, e => e.FieldName == "Preta");
        Assert.Equal("P", preta.Code);
        Assert.Null(preta.UpdatedAt);
    }

    /// <summary>
    /// Additional coverage (T061) for the <c>ToUpdate</c> persistence path: a manifest
    /// member whose <c>description</c> changes between two passes must update the
    /// existing row's compared fields, stamp <c>UpdatedAt</c>, and recompute
    /// <c>ContentHash</c> — while leaving <c>Code</c>, <c>NumericValue</c>,
    /// <c>EnumKey</c> and <c>FieldName</c> untouched.
    /// </summary>
    private static async Task RunUpdateScenarioAsync(DbContext context)
    {
        var store = new EfDataDictionaryStore(context, new DataDictionaryOptions(Manifests: []));

        // First pass: Preta with its original description.
        var firstManifest = BuildManifest(BuildPretaMember(description: "Cor preta"));
        var firstSynchronizer = BuildSynchronizer(store, firstManifest);
        await firstSynchronizer.SynchronizeAsync(CancellationToken.None);

        var afterFirstPass = await store.GetCurrentAsync(EnumKey, CancellationToken.None);
        var pretaAfterFirstPass = Assert.Single(afterFirstPass.Entries);
        Assert.Equal("Cor preta", pretaAfterFirstPass.Description);
        Assert.Null(pretaAfterFirstPass.UpdatedAt);

        // Second pass: same DbContext/store, description changed.
        var secondManifest = BuildManifest(BuildPretaMember(description: "Cor preta (atualizada)"));
        var secondSynchronizer = BuildSynchronizer(store, secondManifest);
        await secondSynchronizer.SynchronizeAsync(CancellationToken.None);

        var afterSecondPass = await store.GetCurrentAsync(EnumKey, CancellationToken.None);
        var preta = Assert.Single(afterSecondPass.Entries);

        Assert.Equal("Cor preta (atualizada)", preta.Description);
        Assert.NotNull(preta.UpdatedAt);
        Assert.True(preta.UpdatedAt >= preta.CreatedAt);
        Assert.Equal(
            DictionaryEntryHasher.Compute("Cor preta (atualizada)", null, 0, false),
            preta.ContentHash);

        Assert.Equal("P", preta.Code);
        Assert.Equal(2, preta.NumericValue);
        Assert.Equal(EnumKey, preta.EnumKey);
        Assert.Equal("Preta", preta.FieldName);
        Assert.Equal(pretaAfterFirstPass.CreatedAt, preta.CreatedAt);
    }

    public sealed class SqlServer : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task Sync_RestartWithNewMember_InsertsOnlyNewRow_LeavesExistingRowUntouched()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await RunScenarioBAsync(context);
        }

        [Fact]
        public async Task Sync_RestartWithChangedDescription_UpdatesRow_StampsUpdatedAt()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await RunUpdateScenarioAsync(context);
        }
    }

    public sealed class PostgreSql : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container =
            new PostgreSqlBuilder("postgres:16-alpine").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task Sync_RestartWithNewMember_InsertsOnlyNewRow_LeavesExistingRowUntouched()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await RunScenarioBAsync(context);
        }

        [Fact]
        public async Task Sync_RestartWithChangedDescription_UpdatesRow_StampsUpdatedAt()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await RunUpdateScenarioAsync(context);
        }
    }
}
