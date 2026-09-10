using DataDictionary.Abstractions.Configuration;
using DataDictionary.Abstractions.Manifest;
using DataDictionary.Core.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace DataDictionary.Core.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to configure and register
/// the DataDictionary services into the DI container.
/// </summary>
public static class DataDictionaryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DataDictionary services, including the synchronization service
    /// and the diff engine, allowing one or more compiled manifests to be registered
    /// via the builder callback.
    /// </summary>
    /// <remarks>
    /// This registers <see cref="DictionaryDiffEngine"/> and
    /// <see cref="DataDictionarySynchronizer"/> only. It does <em>not</em> register
    /// <c>IDataDictionaryStore</c> — <c>Core</c> never references an ORM (Constitution
    /// Principle III), so the consumer (or a provider package such as
    /// <c>DataDictionary.EntityFrameworkCore</c>) is responsible for registering its own
    /// <c>IDataDictionaryStore</c> implementation.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A callback to configure the data dictionary builder,
    /// normally used to register one or more manifests via <see cref="IDataDictionaryBuilder.AddManifest"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDataDictionary(
        this IServiceCollection services,
        Action<IDataDictionaryBuilder> configure)
    {
        // Create the builder to collect manifests
        var builder = new DataDictionaryBuilder(services);

        // Allow the consumer to register manifests
        configure(builder);

        // Register the options type so it can be resolved at runtime
        services.AddSingleton(builder.BuildOptions());

        // The diff engine is stateless and pure; a single shared instance is fine.
        services.AddSingleton<DictionaryDiffEngine>();

        // Likewise stateless (its only state is the injectable warning sink); a single
        // shared instance is fine.
        services.AddSingleton<BreakingChangePolicyEngine>();

        // Scoped to match the typical lifetime of the consumer-registered
        // IDataDictionaryStore (e.g. one backed by a scoped DbContext).
        services.AddScoped<DataDictionarySynchronizer>();

        return services;
    }
}

/// <summary>
/// Builder interface for configuring DataDictionary, allowing registration of
/// one or more compiled manifests (typically from different assemblies).
/// </summary>
public interface IDataDictionaryBuilder
{
    /// <summary>
    /// Registers a compiled manifest (normally the generated <c>Default</c> static
    /// from <c>&lt;ConsumerAssembly&gt;.Generated.DataDictionaryManifest</c>).
    /// Supporting multiple calls allows a host process composed of multiple assemblies,
    /// each with its own generated manifest, to register all of them.
    /// </summary>
    /// <param name="manifest">The manifest to register.</param>
    /// <returns>This builder for chaining.</returns>
    IDataDictionaryBuilder AddManifest(DataDictionaryManifest manifest);

    /// <summary>
    /// Configures the <see cref="SyncMode"/> the startup synchronizer runs
    /// under. Defaults to <see cref="SyncMode.Off"/> when never called, so
    /// adopting the library stays inert until an environment explicitly opts in.
    /// </summary>
    /// <param name="mode">The synchronization mode to configure.</param>
    /// <returns>This builder for chaining.</returns>
    IDataDictionaryBuilder WithSyncMode(SyncMode mode);
}

/// <summary>
/// Default implementation of <see cref="IDataDictionaryBuilder"/>.
/// </summary>
internal sealed class DataDictionaryBuilder : IDataDictionaryBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<DataDictionaryManifest> _manifests = new();
    private SyncMode _syncMode = SyncMode.Off;
    private OnBreakingChange _onBreakingChange = OnBreakingChange.Fail;

    /// <summary>
    /// Creates a new instance of the builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public DataDictionaryBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    public IDataDictionaryBuilder AddManifest(DataDictionaryManifest manifest)
    {
        _manifests.Add(manifest);
        return this;
    }

    /// <inheritdoc/>
    public IDataDictionaryBuilder WithSyncMode(SyncMode mode)
    {
        _syncMode = mode;
        return this;
    }

    /// <summary>
    /// Builds the <see cref="DataDictionaryOptions"/> from the collected configuration.
    /// </summary>
    /// <returns>The configured options.</returns>
    internal DataDictionaryOptions BuildOptions()
    {
        return new DataDictionaryOptions(
            Manifests: _manifests.ToArray(),
            SyncMode: _syncMode,
            OnBreakingChange: _onBreakingChange);
    }
}
