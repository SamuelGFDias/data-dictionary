using DataDictionary.Abstractions.Configuration;
using DataDictionary.Abstractions.Manifest;

namespace DataDictionary.Core;

/// <summary>
/// Runtime configuration for the DataDictionary library, resolved from the DI container
/// and populated during the <see cref="DependencyInjection.DataDictionaryServiceCollectionExtensions.AddDataDictionary"/>
/// configuration step.
/// </summary>
/// <remarks>
/// Immutable after construction. Contains:
/// - All registered manifests (typically one per consuming assembly)
/// - The configured <see cref="SyncMode"/> (default <see cref="SyncMode.Off"/>)
/// - The configured <see cref="OnBreakingChange"/> policy (default <see cref="OnBreakingChange.Fail"/>)
///
/// The diff engine and synchronization service consume this at application startup.
/// </remarks>
/// <param name="Manifests">All registered manifests from the builder configuration.</param>
/// <param name="SyncMode">The configured synchronization mode; defaults to <see cref="SyncMode.Off"/>.</param>
/// <param name="OnBreakingChange">The configured breaking change policy; defaults to <see cref="OnBreakingChange.Fail"/>.</param>
public sealed record DataDictionaryOptions(
    DataDictionaryManifest[] Manifests,
    SyncMode SyncMode = SyncMode.Off,
    OnBreakingChange OnBreakingChange = OnBreakingChange.Fail);
