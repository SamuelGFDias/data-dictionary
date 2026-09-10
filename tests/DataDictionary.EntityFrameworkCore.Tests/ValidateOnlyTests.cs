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
/// Integration coverage (T051) for <c>quickstart.md</c> Scenario E against real,
/// ephemeral SQL Server and PostgreSQL instances, end-to-end through
/// <see cref="DataDictionarySynchronizer"/> (not just <see cref="EfDataDictionaryStore"/>
/// in isolation): starting from Scenario A's synced state (one <c>RacaCor|Branca|B|1</c>
/// row), a new manifest member not yet synced (a plain <c>ToInsert</c> divergence, mirroring
/// Scenario B but without ever syncing it) makes <see cref="SyncMode.ValidateOnly"/> fail
/// the boot with <see cref="DataDictionaryValidationException"/> — and the dictionary
/// table must come out byte-for-byte unchanged: same row, same <c>updated_at</c>.
/// </summary>
public sealed class ValidateOnlyTests
{
    private const string EnumKey = "RacaCor";
    private const string FieldName = "Branca";
    private const string Code = "B";
    private const long NumericValue = 1;
    private const string Description = "Branca";
    private const string GroupName = "Cadastro";

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
        ContentHash = DictionaryEntryHasher.Compute(Description, GroupName, 0, false),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// The manifest handed to <see cref="ValidateOnly"/>: <c>Branca</c> (already synced)
    /// plus a brand-new <c>Preta</c> member that has never been synced, so
    /// <see cref="DictionaryDiffEngine"/> classifies the enum's outcome as a plain
    /// <c>ToInsert</c> divergence with no breaking change at all.
    /// </summary>
    private static DataDictionaryManifest BuildManifestWithUnSyncedMember() => new(
    [
        new ManifestEnumEntry(
            EnumKey: EnumKey,
            GroupName: GroupName,
            ClrFullName: "DataDictionary.EntityFrameworkCore.Tests.ValidateOnlyTests+RacaCor",
            AssemblyName: typeof(ValidateOnlyTests).Assembly.GetName().Name!,
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
                new ManifestMemberEntry(
                    FieldName: "Preta",
                    Code: "P",
                    NumericValue: 2,
                    Description: "Preta",
                    GroupName: GroupName,
                    IsDeprecated: false,
                    SortOrder: 1),
            ]),
    ]);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyDataDictionary();
        }
    }

    private static async Task RunScenarioAsync(DbContext context)
    {
        var seedManifest = new DataDictionaryManifest([]);
        var store = new EfDataDictionaryStore(context, new DataDictionaryOptions(Manifests: [seedManifest]));

        // Scenario A's starting state: one synced dictionary row.
        await store.ApplyAsync(
            new SynchronizationOutcome(
                ToInsert: [BuildBrancaEntry()],
                ToUpdate: [],
                ToDeactivate: [],
                BreakingChanges: [],
                Unchanged: []),
            CancellationToken.None);

        var beforeState = await store.GetCurrentAsync(EnumKey, CancellationToken.None);
        var beforeEntry = Assert.Single(beforeState.Entries);

        var manifest = BuildManifestWithUnSyncedMember();
        var options = new DataDictionaryOptions(
            Manifests: [manifest],
            SyncMode: SyncMode.ValidateOnly,
            OnBreakingChange: OnBreakingChange.Fail);

        var synchronizer = new DataDictionarySynchronizer(
            options,
            store,
            new DictionaryDiffEngine(),
            new BreakingChangePolicyEngine());

        var exception = await Assert.ThrowsAsync<DataDictionaryValidationException>(
            () => synchronizer.SynchronizeAsync(CancellationToken.None));

        Assert.Equal(EnumKey, exception.EnumKey);
        Assert.Single(exception.Outcome.ToInsert);
        Assert.Equal("Preta", exception.Outcome.ToInsert[0].FieldName);
        Assert.Empty(exception.Outcome.ToUpdate);
        Assert.Empty(exception.Outcome.ToDeactivate);
        Assert.Empty(exception.Outcome.BreakingChanges);
        Assert.Contains(EnumKey, exception.Message, StringComparison.Ordinal);

        // The dictionary table must come out byte-for-byte unchanged: same single row,
        // same values, same updated_at.
        var afterState = await store.GetCurrentAsync(EnumKey, CancellationToken.None);
        var afterEntry = Assert.Single(afterState.Entries);

        Assert.Equal(beforeEntry.EnumKey, afterEntry.EnumKey);
        Assert.Equal(beforeEntry.FieldName, afterEntry.FieldName);
        Assert.Equal(beforeEntry.Code, afterEntry.Code);
        Assert.Equal(beforeEntry.NumericValue, afterEntry.NumericValue);
        Assert.Equal(beforeEntry.Description, afterEntry.Description);
        Assert.Equal(beforeEntry.GroupName, afterEntry.GroupName);
        Assert.Equal(beforeEntry.IsActive, afterEntry.IsActive);
        Assert.Equal(beforeEntry.IsDeprecated, afterEntry.IsDeprecated);
        Assert.Equal(beforeEntry.SortOrder, afterEntry.SortOrder);
        Assert.Equal(beforeEntry.ContentHash, afterEntry.ContentHash);
        Assert.Equal(beforeEntry.CreatedAt, afterEntry.CreatedAt);
        Assert.Equal(beforeEntry.UpdatedAt, afterEntry.UpdatedAt);
        Assert.Null(afterEntry.UpdatedAt);

        // No new row for "Preta" was inserted, and no catalog entry was touched either.
        Assert.DoesNotContain(afterState.Entries, e => e.FieldName == "Preta");
    }

    public sealed class SqlServer : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task ValidateOnly_AnyDivergence_ThrowsDataDictionaryValidationException_NeverWrites()
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
        public async Task ValidateOnly_AnyDivergence_ThrowsDataDictionaryValidationException_NeverWrites()
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
