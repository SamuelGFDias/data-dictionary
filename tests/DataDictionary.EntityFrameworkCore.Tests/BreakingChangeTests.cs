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
/// Integration coverage (T050) for <c>quickstart.md</c> Scenario C against real,
/// ephemeral SQL Server and PostgreSQL instances, end-to-end through
/// <see cref="DataDictionarySynchronizer"/> (not just <see cref="EfDataDictionaryStore"/>
/// in isolation): starting from Scenario A's state (one synced <c>RacaCor|Branca|B|1</c>
/// row) plus a business row (<see cref="Pessoa"/>) referencing code <c>B</c>, removing
/// <c>Branca</c> from the manifest handed to the synchronizer and running
/// <see cref="SyncMode.Sync"/> must fail the boot with
/// <see cref="DataDictionarySyncException"/> — naming the enum, the removed member, its
/// code, and the referencing business table — and must not write anything to the
/// dictionary table (<c>RacaCor.IsCodeInUseAsync</c>, T052, resolving "still in use" via
/// a real scan of <see cref="DbContext.Model"/>, per <c>research.md</c> §7).
/// </summary>
/// <remarks>
/// The library's own composition (see <c>Sample.Api/Program.cs</c>) wires
/// <see cref="EfDataDictionaryStore"/> with the very same <see cref="DataDictionaryOptions"/>
/// used to drive the synchronizer's diff loop. Here the two are deliberately built from
/// different manifest instances of the same content: the store's options keep the full,
/// still-current <c>RacaCor</c> manifest (matching the actual <see cref="RacaCor"/> CLR
/// type, which still declares every member) so <see cref="EfDataDictionaryStore.IsCodeInUseAsync"/>
/// can resolve code <c>B</c> back to <see cref="RacaCor.Branca"/> and query
/// <see cref="Pessoa"/> for it, while the synchronizer's options carry the reduced
/// manifest simulating "the developer removed the <c>Branca</c> member from the source
/// enum's manifest declaration" — the actual trigger <see cref="DictionaryDiffEngine"/>
/// reacts to (a stored, active <c>field_name</c> absent from the manifest being synced).
/// </remarks>
public sealed class BreakingChangeTests
{
    private const string EnumKey = "RacaCor";
    private const string FieldName = "Branca";
    private const string Code = "B";
    private const long NumericValue = 1;
    private const string Description = "Branca";
    private const string GroupName = "Cadastro";
    private const string BusinessTableName = nameof(Pessoa);

    /// <summary>A minimal enum standing in for the sample's own <c>RacaCor</c>.</summary>
    private enum RacaCor
    {
        Branca = 1,
    }

    /// <summary>
    /// A fictional business entity whose column is mapped as <see cref="RacaCor"/> via
    /// <see cref="EnumCodeValueConverter{TEnum}"/> — the "business data" whose reference
    /// to code <c>B</c> must block the removal.
    /// </summary>
    private sealed class Pessoa
    {
        public int Id { get; set; }

        public RacaCor RacaCor { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, DataDictionaryManifest fullManifest)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pessoa>();
            modelBuilder.ApplyDataDictionary();
            modelBuilder.ApplyEnumCodeConverters(fullManifest);
        }
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

        // A business row referencing the dictionary code that is about to be removed.
        context.Set<Pessoa>().Add(new Pessoa { RacaCor = RacaCor.Branca });
        await context.SaveChangesAsync();

        // The store used by the synchronizer keeps the full, still-current manifest so
        // IsCodeInUseAsync can resolve code 'B' back to RacaCor.Branca and scan Pessoa
        // for it — see the class remarks.
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

        var exception = await Assert.ThrowsAsync<DataDictionarySyncException>(
            () => synchronizer.SynchronizeAsync(CancellationToken.None));

        Assert.Contains(EnumKey, exception.Message, StringComparison.Ordinal);
        Assert.Contains(FieldName, exception.Message, StringComparison.Ordinal);
        Assert.Contains(Code, exception.Message, StringComparison.Ordinal);
        Assert.Contains(BusinessTableName, exception.Message, StringComparison.Ordinal);

        var breakingChange = Assert.Single(exception.BreakingChanges);
        Assert.Equal(BreakingChangeReason.InUseCodeRemoved, breakingChange.Reason);
        Assert.Contains(BusinessTableName, breakingChange.ReferencingTables);

        // Nothing was written: the original dictionary row is unchanged.
        var afterState = await store.GetCurrentAsync(EnumKey, CancellationToken.None);
        var entry = Assert.Single(afterState.Entries);
        Assert.Equal(FieldName, entry.FieldName);
        Assert.Equal(Code, entry.Code);
        Assert.True(entry.IsActive);
        Assert.Null(entry.UpdatedAt);
    }

    public sealed class SqlServer : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task Sync_RemovedMemberCodeStillInUse_ThrowsDataDictionarySyncException_NeverWrites()
        {
            var fullManifest = BuildFullManifest();
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options, fullManifest);
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
        public async Task Sync_RemovedMemberCodeStillInUse_ThrowsDataDictionarySyncException_NeverWrites()
        {
            var fullManifest = BuildFullManifest();
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options, fullManifest);
            await context.Database.EnsureCreatedAsync();

            await RunScenarioAsync(context);
        }
    }
}
