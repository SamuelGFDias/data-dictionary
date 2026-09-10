using DataDictionary.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DataDictionary.EntityFrameworkCore.Tests;

/// <summary>
/// Integration coverage for <see cref="ModelBuilderExtensions.ApplyDataDictionary"/>
/// (T038) against real, ephemeral SQL Server and PostgreSQL instances, per
/// <c>research.md</c> §9 ("provider = teste de integração com Testcontainers contra banco
/// real") — an in-memory provider cannot exercise real unique-index behavior. Asserts the
/// expected tables, columns, and the <c>(enum_key, code)</c> filtered unique index (active
/// rows only) all exist exactly as <c>data-model.md</c> describes.
/// </summary>
public sealed class SchemaMappingTests
{
    private const string DictionaryTable = "tb_dicionario_dados";
    private const string EnumCatalogTable = "tb_dicionario_enum";

    private static readonly string[] ExpectedDictionaryColumns =
    [
        "enum_key", "field_name", "code", "numeric_value", "description", "group_name",
        "is_active", "is_deprecated", "sort_order", "content_hash", "created_at", "updated_at",
    ];

    private static readonly string[] ExpectedEnumCatalogColumns =
    [
        "enum_key", "clr_full_name", "assembly_name", "description", "group_name",
        "manifest_hash", "last_sync_at",
    ];

    /// <summary>
    /// The consumer <see cref="DbContext"/> under test — its entire model comes from
    /// <see cref="ModelBuilderExtensions.ApplyDataDictionary"/>, exactly as a real consumer
    /// would call it inside their own <c>OnModelCreating</c>.
    /// </summary>
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyDataDictionary();
        }
    }

    private static async Task<List<string>> GetColumnNamesAsync(DbContext context, string tableName) =>
        await context.Database
            .SqlQueryRaw<string>(
                "SELECT COLUMN_NAME AS \"Value\" FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {0}",
                tableName)
            .ToListAsync();

    private static async Task<bool> TableExistsAsync(DbContext context, string tableName)
    {
        var count = await context.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS \"Value\" FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = {0}",
                tableName)
            .SingleAsync();

        return count > 0;
    }

    private static async Task AssertTableShapeAsync(
        DbContext context,
        string tableName,
        IReadOnlyCollection<string> expectedColumns)
    {
        Assert.True(await TableExistsAsync(context, tableName), $"Table '{tableName}' was not created.");

        var actualColumns = await GetColumnNamesAsync(context, tableName);

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.Contains(expectedColumn, actualColumns);
        }
    }

    private static async Task AssertSqlServerFilteredUniqueIndexAsync(DbContext context)
    {
        var filterDefinitions = await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT i.filter_definition AS "Value"
                FROM sys.indexes i
                JOIN sys.tables t ON i.object_id = t.object_id
                WHERE t.name = {0} AND i.is_unique = 1 AND i.has_filter = 1
                """,
                DictionaryTable)
            .ToListAsync();

        Assert.Contains(
            filterDefinitions,
            filter => filter.Contains("is_active", StringComparison.OrdinalIgnoreCase));

        var indexedColumns = await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT c.name AS "Value"
                FROM sys.indexes i
                JOIN sys.tables t ON i.object_id = t.object_id
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE t.name = {0} AND i.is_unique = 1 AND i.has_filter = 1
                """,
                DictionaryTable)
            .ToListAsync();

        Assert.Contains("enum_key", indexedColumns);
        Assert.Contains("code", indexedColumns);
    }

    private static async Task AssertPostgreSqlFilteredUniqueIndexAsync(DbContext context)
    {
        var indexDefinitions = await context.Database
            .SqlQueryRaw<string>(
                "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = {0}",
                DictionaryTable)
            .ToListAsync();

        Assert.Contains(indexDefinitions, def =>
            def.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            && def.Contains("WHERE", StringComparison.OrdinalIgnoreCase)
            && def.Contains("is_active", StringComparison.OrdinalIgnoreCase)
            && def.Contains("enum_key", StringComparison.OrdinalIgnoreCase)
            && def.Contains("code", StringComparison.OrdinalIgnoreCase));
    }

    public sealed class SqlServer : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task ApplyDataDictionary_CreatesExpectedTablesColumnsAndFilteredIndex()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await AssertTableShapeAsync(context, DictionaryTable, ExpectedDictionaryColumns);
            await AssertTableShapeAsync(context, EnumCatalogTable, ExpectedEnumCatalogColumns);
            await AssertSqlServerFilteredUniqueIndexAsync(context);
        }
    }

    public sealed class PostgreSql : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container =
            new PostgreSqlBuilder("postgres:16-alpine").Build();

        public Task InitializeAsync() => _container.StartAsync();

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        [Fact]
        public async Task ApplyDataDictionary_CreatesExpectedTablesColumnsAndFilteredIndex()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;

            await using var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await AssertTableShapeAsync(context, DictionaryTable, ExpectedDictionaryColumns);
            await AssertTableShapeAsync(context, EnumCatalogTable, ExpectedEnumCatalogColumns);
            await AssertPostgreSqlFilteredUniqueIndexAsync(context);
        }
    }
}
