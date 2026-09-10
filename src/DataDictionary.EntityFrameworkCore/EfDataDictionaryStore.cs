using System.Linq.Expressions;
using System.Reflection;
using DataDictionary.Abstractions;
using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Persistence;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core;
using Microsoft.EntityFrameworkCore;

namespace DataDictionary.EntityFrameworkCore;

/// <summary>
/// The EF Core implementation of <see cref="IDataDictionaryStore"/> — the seam
/// through which <c>DataDictionary.Core</c>'s diff/orchestration engine reads and
/// writes the dictionary tables mapped by
/// <see cref="ModelBuilderExtensions.ApplyDataDictionary"/> (T038). See
/// <c>contracts/store-contract.md</c>.
/// </summary>
/// <remarks>
/// This phase (User Story 1, T044/T045) implements <see cref="GetCurrentAsync"/>
/// and the insert-only path of <see cref="ApplyAsync"/> — enough for a first sync
/// against an empty database (<c>quickstart.md</c> Scenario A). <see cref="ApplyAsync"/>
/// throws <see cref="NotSupportedException"/> if handed an outcome carrying updates or
/// deactivations, since that classification does not exist in the diff engine yet
/// either. User Story 2 (T052) adds <see cref="IsCodeInUseAsync"/>, which walks the
/// consumer's own <see cref="DbContext.Model"/> per <c>research.md</c> §7.
/// <see cref="AcquireLockAsync"/> remains out of scope for this phase (later user
/// story — concurrent-replica-boot locking) and throws
/// <see cref="NotImplementedException"/> until then.
/// </remarks>
/// <param name="dbContext">
/// The consumer's own <see cref="DbContext"/> — the same instance whose
/// <c>OnModelCreating</c> called <see cref="ModelBuilderExtensions.ApplyDataDictionary"/>,
/// so <see cref="DictionaryEntry"/> and <see cref="DictionaryEnumCatalogEntry"/> are
/// already part of its model.
/// </param>
/// <param name="options">
/// The library's resolved <see cref="DataDictionaryOptions"/> — used by
/// <see cref="IsCodeInUseAsync"/> to resolve the CLR enum type behind a given
/// <c>enumKey</c> from the registered <see cref="DataDictionaryOptions.Manifests"/>.
/// </param>
public sealed class EfDataDictionaryStore(DbContext dbContext, DataDictionaryOptions options) : IDataDictionaryStore
{
    private static readonly MethodInfo AnyEntityUsesValueMethod = typeof(EfDataDictionaryStore)
        .GetMethod(nameof(AnyEntityUsesValueAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly DataDictionaryOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public async Task<CurrentDictionaryState> GetCurrentAsync(
        string enumKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(enumKey);

        var entries = await _dbContext.Set<DictionaryEntry>()
            .Where(e => e.EnumKey == enumKey)
            .ToListAsync(cancellationToken);

        var catalogEntry = await _dbContext.Set<DictionaryEnumCatalogEntry>()
            .SingleOrDefaultAsync(e => e.EnumKey == enumKey, cancellationToken);

        return new CurrentDictionaryState(enumKey, entries, catalogEntry);
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">
    /// <paramref name="outcome"/> carries any <see cref="SynchronizationOutcome.ToUpdate"/>
    /// or <see cref="SynchronizationOutcome.ToDeactivate"/> entries — not supported until
    /// a later user story implements that classification and its persistence.
    /// </exception>
    public async Task ApplyAsync(
        SynchronizationOutcome outcome,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.ToUpdate.Count > 0 || outcome.ToDeactivate.Count > 0)
        {
            throw new NotSupportedException(
                "EfDataDictionaryStore.ApplyAsync currently supports insert-only " +
                "outcomes (User Story 1 scope). Update and deactivate support land in " +
                "later user stories.");
        }

        if (outcome.ToInsert.Count == 0)
        {
            return;
        }

        // A single SaveChangesAsync call commits every ToInsert row for this enum in
        // one database transaction, satisfying the "apply the whole outcome
        // atomically per enum" contract.
        await _dbContext.Set<DictionaryEntry>().AddRangeAsync(outcome.ToInsert, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Per <c>research.md</c> §7: resolves the CLR enum type registered for
    /// <paramref name="enumKey"/> from <see cref="DataDictionaryOptions.Manifests"/>, walks
    /// <see cref="DbContext.Model"/> for every mapped entity property of that CLR type, and
    /// queries each such entity set for a row currently holding the enum member that
    /// resolves to <paramref name="code"/> (comparing in terms of the enum value, so any
    /// <c>ValueConverter</c> applied to the property — e.g. <see cref="EnumCodeValueConverter{TEnum}"/>
    /// — is honored transparently by EF's LINQ translation). If <paramref name="enumKey"/>
    /// is not found in any registered manifest, or <paramref name="code"/> does not match
    /// any member of that enum, the result is <c>InUse: false</c>.
    /// </remarks>
    public async Task<CodeUsageResult> IsCodeInUseAsync(
        string enumKey,
        string code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(enumKey);
        ArgumentNullException.ThrowIfNull(code);

        var enumEntry = _options.Manifests
            .SelectMany(manifest => manifest.Enums)
            .FirstOrDefault(entry => entry.EnumKey == enumKey);

        if (enumEntry is null)
        {
            return new CodeUsageResult(InUse: false, ReferencingTables: []);
        }

        var enumType = ResolveEnumType(enumEntry);

        if (enumType is null)
        {
            return new CodeUsageResult(InUse: false, ReferencingTables: []);
        }

        var member = enumEntry.Members.FirstOrDefault(m => m.Code == code);

        if (member is null)
        {
            return new CodeUsageResult(InUse: false, ReferencingTables: []);
        }

        var enumValue = Enum.Parse(enumType, member.FieldName);

        var referencingTables = new List<string>();

        foreach (var entityType in _dbContext.Model.GetEntityTypes())
        {
            if (entityType.ClrType is null || entityType.IsOwned())
            {
                continue;
            }

            var tableName = entityType.GetTableName();

            if (tableName is null)
            {
                // Not mapped to a table (e.g. a keyless view or TPH-derived type without
                // its own table) — nothing to query.
                continue;
            }

            var matchingProperties = entityType.GetProperties()
                .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == enumType)
                .ToList();

            if (matchingProperties.Count == 0)
            {
                continue;
            }

            var alreadyReferenced = false;

            foreach (var property in matchingProperties)
            {
                if (alreadyReferenced)
                {
                    break;
                }

                var inUseTask = (Task<bool>)AnyEntityUsesValueMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [property.Name, property.ClrType, enumValue, cancellationToken])!;

                if (await inUseTask)
                {
                    alreadyReferenced = true;
                }
            }

            if (alreadyReferenced)
            {
                var schema = entityType.GetSchema();
                referencingTables.Add(schema is null ? tableName : $"{schema}.{tableName}");
            }
        }

        return new CodeUsageResult(
            InUse: referencingTables.Count > 0,
            ReferencingTables: referencingTables);
    }

    /// <summary>
    /// Resolves the CLR <see cref="Type"/> a <see cref="ManifestEnumEntry"/> describes,
    /// the same way <see cref="EnumCodeValueConverterExtensions"/> does — via
    /// <see cref="Type.GetType(string, bool)"/> against the assembly-qualified name built
    /// from <see cref="ManifestEnumEntry.ClrFullName"/> and
    /// <see cref="ManifestEnumEntry.AssemblyName"/>.
    /// </summary>
    private static Type? ResolveEnumType(ManifestEnumEntry enumEntry)
    {
        var assemblyQualifiedName = $"{enumEntry.ClrFullName}, {enumEntry.AssemblyName}";
        var type = Type.GetType(assemblyQualifiedName, throwOnError: false);
        return type is { IsEnum: true } ? type : null;
    }

    /// <summary>
    /// Queries whether any <typeparamref name="TEntity"/> row currently holds
    /// <paramref name="enumValue"/> in its <paramref name="propertyName"/> property,
    /// built as a dynamic LINQ expression since the property's enum type is only known at
    /// runtime (from the manifest), not at compile time.
    /// </summary>
    private async Task<bool> AnyEntityUsesValueAsync<TEntity>(
        string propertyName,
        Type propertyType,
        object enumValue,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var property = Expression.Property(parameter, propertyName);

        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        Expression valueExpression = Expression.Constant(enumValue, underlyingType);

        if (propertyType != underlyingType)
        {
            valueExpression = Expression.Convert(valueExpression, propertyType);
        }

        var predicate = Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(property, valueExpression),
            parameter);

        return await _dbContext.Set<TEntity>().AnyAsync(predicate, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">
    /// Always — the distributed advisory lock used for concurrent-replica boot is
    /// implemented in a later user story.
    /// </exception>
    public Task<LockAcquisitionResult> AcquireLockAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "AcquireLockAsync is out of scope for User Story 1 (T044/T045) and is " +
            "implemented in the user story covering concurrent replica boot.");
}
