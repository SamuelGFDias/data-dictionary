using DataDictionary.Abstractions.Persistence;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DataDictionary.EntityFrameworkCore.Tests;

/// <summary>
/// Integration coverage (T042) for <c>quickstart.md</c> Scenario A against real,
/// ephemeral SQL Server and PostgreSQL instances: an empty database, the
/// <c>RacaCor</c> enum's single <c>Branca = 1</c> / code <c>B</c> member applied via
/// <see cref="EfDataDictionaryStore.ApplyAsync"/> in <c>Sync</c> mode, asserting the
/// exact resulting row <c>RacaCor | Branca | B | 1 | Branca</c>. Also exercises
/// <see cref="EfDataDictionaryStore.GetCurrentAsync"/> (T044) reading that same row
/// back, matching what the startup synchronizer would do on a subsequent boot.
/// </summary>
public sealed class FreshSyncTests
{
    private const string EnumKey = "RacaCor";
    private const string FieldName = "Branca";
    private const string Code = "B";
    private const long NumericValue = 1;
    private const string Description = "Branca";
    private const string GroupName = "Cadastro";

    /// <summary>
    /// The consumer <see cref="DbContext"/> under test — its entire model comes from
    /// <see cref="ModelBuilderExtensions.ApplyDataDictionary"/>, exactly as a real
    /// consumer would call it inside their own <c>OnModelCreating</c>.
    /// </summary>
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyDataDictionary();
        }
    }

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

    private static async Task RunScenarioAAsync(DbContext context)
    {
        var store = new EfDataDictionaryStore(context, new DataDictionaryOptions(Manifests: []));

        // Empty database: GetCurrentAsync must report nothing for this enum yet.
        var beforeState = await store.GetCurrentAsync(EnumKey, CancellationToken.None);
        Assert.Empty(beforeState.Entries);

        var outcome = new SynchronizationOutcome(
            ToInsert: [BuildBrancaEntry()],
            ToUpdate: [],
            ToDeactivate: [],
            BreakingChanges: [],
            Unchanged: []);

        await store.ApplyAsync(outcome, CancellationToken.None);

        var afterState = await store.GetCurrentAsync(EnumKey, CancellationToken.None);

        var entry = Assert.Single(afterState.Entries);
        Assert.Equal(EnumKey, entry.EnumKey);
        Assert.Equal(FieldName, entry.FieldName);
        Assert.Equal(Code, entry.Code);
        Assert.Equal(NumericValue, entry.NumericValue);
        Assert.Equal(Description, entry.Description);
    }

    public sealed class SqlServer : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task FreshSync_InsertsExpectedSingleRow()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await RunScenarioAAsync(context);
        }
    }

    public sealed class PostgreSql : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container =
            new PostgreSqlBuilder("postgres:16-alpine").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task FreshSync_InsertsExpectedSingleRow()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await RunScenarioAAsync(context);
        }
    }
}
