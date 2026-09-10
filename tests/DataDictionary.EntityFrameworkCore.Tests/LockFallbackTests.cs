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
/// Integration coverage (T067) for <c>quickstart.md</c> Scenario F's fallback path
/// against real, ephemeral SQL Server and PostgreSQL instances: with the FR-024
/// coordination lock already held by another connection, a <see
/// cref="SyncMode.Sync"/> replica that cannot obtain it must fall back to the same
/// validate-and-throw-on-divergence behavior as <see cref="SyncMode.ValidateOnly"/> —
/// never writing and never failing boot merely because the lock was unavailable. To
/// isolate that from the ordinary "lock held → wait → observe no divergence" case, the
/// database is seeded to already match the manifest (via a first store, outside the
/// synchronizer) BEFORE that same store takes the lock, so the second replica's
/// validate-only pass has nothing to diverge on and boot succeeds without writing.
/// </summary>
/// <remarks>
/// <see cref="DataDictionarySynchronizer"/>'s coordination-lock timeout is a fixed,
/// non-configurable 5 seconds — this test's second replica genuinely waits out that
/// timeout before falling back, so each <c>[Fact]</c> here takes several real seconds.
/// That wait is the behavior under test, not incidental slowness.
/// </remarks>
public sealed class LockFallbackTests
{
    private const string EnumKey = "RacaCor";
    private const string FieldName = "Branca";
    private const string Code = "B";
    private const long NumericValue = 1;
    private const string Description = "Branca";
    private const string GroupName = "Cadastro";

    /// <summary>
    /// The consumer <see cref="DbContext"/> under test — no business entity is mapped,
    /// mirroring <c>RetirementTests.TestDbContext</c>.
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
    /// <see cref="ApplyAsync"/> actually ran, so the test can assert the fallback
    /// (lock-losing) replica never wrote.
    /// </summary>
    private sealed class ApplyCountingStore(IDataDictionaryStore inner) : IDataDictionaryStore
    {
        public int ApplyCount { get; private set; }

        public Task<CurrentDictionaryState> GetCurrentAsync(string enumKey, CancellationToken cancellationToken) =>
            inner.GetCurrentAsync(enumKey, cancellationToken);

        public async Task ApplyAsync(SynchronizationOutcome outcome, CancellationToken cancellationToken)
        {
            ApplyCount++;
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

    // Computed the exact same way DictionaryDiffEngine hashes a manifest member, so the
    // seeded row below matches the manifest's content_hash exactly — otherwise the
    // diff engine would (correctly) classify it as ToUpdate, which is a real
    // divergence the fallback replica's validate-only pass must legitimately reject,
    // and would defeat the "no divergence" scenario this file is testing.
    private static readonly string ExpectedContentHash =
        DictionaryEntryHasher.Compute(Description, GroupName, sortOrder: 0, isDeprecated: false);

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
        ContentHash = ExpectedContentHash,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static async Task RunScenarioAsync(
        Func<DbContextOptionsBuilder<TestDbContext>, DbContextOptionsBuilder<TestDbContext>> configureProvider)
    {
        var manifest = BuildFullManifest();

        // First connection/store: seeds the expected state directly via ApplyAsync
        // (bypassing the synchronizer, same as RetirementTests' seedStore) BEFORE it
        // takes the lock, so that once it holds the lock the database already matches
        // the manifest exactly — the second replica's fallback validate-only pass will
        // find no divergence.
        await using var context1 = new TestDbContext(configureProvider(new DbContextOptionsBuilder<TestDbContext>()).Options);
        var store1 = new EfDataDictionaryStore(context1, new DataDictionaryOptions(Manifests: [manifest]));

        await store1.ApplyAsync(
            new SynchronizationOutcome(
                ToInsert: [BuildBrancaEntry()],
                ToUpdate: [],
                ToDeactivate: [],
                BreakingChanges: [],
                Unchanged: []),
            CancellationToken.None);

        // store1 now takes and holds the coordination lock manually, simulating
        // another replica that is mid-synchronization, to force the second replica
        // below to lose the race.
        var lockResult = await store1.AcquireLockAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        Assert.True(lockResult.IsAcquired);
        var lockHandle = lockResult.Handle!;

        try
        {
            // Second connection/store/synchronizer: the actual replica under test. Its
            // AcquireLockAsync call (inside SynchronizeAsync) will wait out the
            // synchronizer's fixed 5s LockTimeout, fail to get the lock (store1 still
            // holds it), and fall back to the validate-only path.
            await using var context2 = new TestDbContext(configureProvider(new DbContextOptionsBuilder<TestDbContext>()).Options);
            var innerStore2 = new EfDataDictionaryStore(context2, new DataDictionaryOptions(Manifests: [manifest]));
            var store2 = new ApplyCountingStore(innerStore2);

            var options = new DataDictionaryOptions(
                Manifests: [manifest],
                SyncMode: SyncMode.Sync,
                OnBreakingChange: OnBreakingChange.Fail);

            var synchronizer2 = new DataDictionarySynchronizer(
                options,
                store2,
                new DictionaryDiffEngine(),
                new BreakingChangePolicyEngine());

            // No exception: the database already matches the manifest, so the
            // fallback validate-only pass finds no divergence and boot succeeds —
            // exactly the "no startup failure caused solely by the lock" outcome
            // quickstart.md's Scenario F requires.
            await synchronizer2.SynchronizeAsync(CancellationToken.None);

            // The fallback replica never wrote.
            Assert.Equal(0, store2.ApplyCount);
        }
        finally
        {
            // Release the manually-held lock so the container isn't left with a
            // dangling session-scoped lock at the end of the test.
            await lockHandle.DisposeAsync();
        }
    }

    public sealed class SqlServer : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task Sync_LockHeldElsewhere_FallsBackToValidateOnly_BootSucceedsWithoutWriting()
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
        public async Task Sync_LockHeldElsewhere_FallsBackToValidateOnly_BootSucceedsWithoutWriting()
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
