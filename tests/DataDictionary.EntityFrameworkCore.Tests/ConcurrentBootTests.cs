using DataDictionary.Abstractions;
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
/// Integration coverage (T066) for <c>quickstart.md</c> Scenario F against real,
/// ephemeral SQL Server and PostgreSQL instances: two simulated replicas — each its
/// own <see cref="DbContext"/>/connection, its own <see cref="EfDataDictionaryStore"/>,
/// its own <see cref="DataDictionarySynchronizer"/> — both in <see cref="SyncMode.Sync"/>
/// against the SAME, initially empty database, starting <see
/// cref="DataDictionarySynchronizer.SynchronizeAsync"/> concurrently. Per FR-024 /
/// User Story 5, exactly one of them should win the coordination lock and perform the
/// write; the other should fall back to the validate-only path and observe no
/// divergence once the winner's write lands, so both boots finish successfully with no
/// unique-constraint violation — matching Scenario A's end state.
/// </summary>
public sealed class ConcurrentBootTests
{
    private const string EnumKey = "RacaCor";
    private const string FieldName = "Branca";
    private const string Code = "B";
    private const long NumericValue = 1;
    private const string Description = "Branca";
    private const string GroupName = "Cadastro";

    /// <summary>
    /// The consumer <see cref="DbContext"/> under test — no business entity is mapped,
    /// mirroring <c>RetirementTests.TestDbContext</c>; irrelevant here since neither
    /// replica removes anything, but kept consistent with the rest of the suite.
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

    /// <summary>
    /// Wraps a real <see cref="IDataDictionaryStore"/> to count how many times
    /// <see cref="ApplyAsync"/> actually wrote something, so the test can observe
    /// which of the two concurrent replicas performed the write (Scenario F: "exactly
    /// one instance actually performed the write"). <see
    /// cref="DataDictionarySynchronizer"/> calls <c>ApplyAsync</c> unconditionally
    /// once it holds the lock, even when the diff found nothing to change (the
    /// second-to-acquire replica, once it observes the first replica's already-applied
    /// state), so only an outcome that actually carries an insert, update or
    /// deactivation counts as a write here.
    /// </summary>
    private sealed class ApplyCountingStore(IDataDictionaryStore inner) : IDataDictionaryStore
    {
        public int ApplyCount { get; private set; }

        public Task<CurrentDictionaryState> GetCurrentAsync(string enumKey, CancellationToken cancellationToken) =>
            inner.GetCurrentAsync(enumKey, cancellationToken);

        public async Task ApplyAsync(SynchronizationOutcome outcome, CancellationToken cancellationToken)
        {
            if (outcome.ToInsert.Count > 0 || outcome.ToUpdate.Count > 0 || outcome.ToDeactivate.Count > 0)
            {
                ApplyCount++;
            }

            await inner.ApplyAsync(outcome, cancellationToken);
        }

        public Task<CodeUsageResult> IsCodeInUseAsync(string enumKey, string code, CancellationToken cancellationToken) =>
            inner.IsCodeInUseAsync(enumKey, code, cancellationToken);

        public Task<LockAcquisitionResult> AcquireLockAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            inner.AcquireLockAsync(timeout, cancellationToken);
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

    /// <summary>
    /// Builds one simulated replica: its own <see cref="TestDbContext"/> (own
    /// connection), its own counting store, its own synchronizer — all wired to the
    /// same <paramref name="connectionStringFactory"/>-produced database.
    /// </summary>
    private static (ApplyCountingStore Store, DataDictionarySynchronizer Synchronizer, TestDbContext Context) BuildReplica(
        Func<DbContextOptionsBuilder<TestDbContext>, DbContextOptionsBuilder<TestDbContext>> configureProvider)
    {
        var manifest = BuildFullManifest();

        var optionsBuilder = configureProvider(new DbContextOptionsBuilder<TestDbContext>());
        var context = new TestDbContext(optionsBuilder.Options);

        var innerStore = new EfDataDictionaryStore(context, new DataDictionaryOptions(Manifests: [manifest]));
        var countingStore = new ApplyCountingStore(innerStore);

        var options = new DataDictionaryOptions(
            Manifests: [manifest],
            SyncMode: SyncMode.Sync,
            OnBreakingChange: OnBreakingChange.Fail);

        var synchronizer = new DataDictionarySynchronizer(
            options,
            countingStore,
            new DictionaryDiffEngine(),
            new BreakingChangePolicyEngine());

        return (countingStore, synchronizer, context);
    }

    private static async Task RunScenarioAsync(
        Func<DbContextOptionsBuilder<TestDbContext>, DbContextOptionsBuilder<TestDbContext>> configureProvider)
    {
        var (store1, synchronizer1, context1) = BuildReplica(configureProvider);
        var (store2, synchronizer2, context2) = BuildReplica(configureProvider);

        await using var _ = context1;
        await using var __ = context2;

        // Database starts empty (unlike RetirementTests/Scenario D, there is no prior
        // seed) — both replicas race to be the one that inserts the row. Neither
        // Task should throw: the loser falls back to validate-only semantics instead
        // of failing boot merely because it lost the race for the lock.
        await Task.WhenAll(
            synchronizer1.SynchronizeAsync(CancellationToken.None),
            synchronizer2.SynchronizeAsync(CancellationToken.None));

        // Exactly one replica performed the write.
        Assert.Equal(1, store1.ApplyCount + store2.ApplyCount);

        // The database ends up in Scenario A's state: one row, no duplicate-key
        // violation (if there had been one, Task.WhenAll above would already have
        // thrown and this assertion would never be reached).
        var finalState = await store1.GetCurrentAsync(EnumKey, CancellationToken.None);
        var entry = Assert.Single(finalState.Entries);
        Assert.Equal(FieldName, entry.FieldName);
        Assert.Equal(Code, entry.Code);
        Assert.True(entry.IsActive);
    }

    public sealed class SqlServer : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task ConcurrentSyncBoots_OnlyOneReplicaWrites_BothSucceed()
        {
            var connectionString = _container.GetConnectionString();

            await using (var setupContext = new TestDbContext(
                new DbContextOptionsBuilder<TestDbContext>().UseSqlServer(connectionString).Options))
            {
                await setupContext.Database.EnsureCreatedAsync();
            }

            await RunScenarioAsync(builder => builder.UseSqlServer(connectionString));
        }
    }

    public sealed class PostgreSql : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container =
            new PostgreSqlBuilder("postgres:16-alpine").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task ConcurrentSyncBoots_OnlyOneReplicaWrites_BothSucceed()
        {
            var connectionString = _container.GetConnectionString();

            await using (var setupContext = new TestDbContext(
                new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(connectionString).Options))
            {
                await setupContext.Database.EnsureCreatedAsync();
            }

            await RunScenarioAsync(builder => builder.UseNpgsql(connectionString));
        }
    }
}
