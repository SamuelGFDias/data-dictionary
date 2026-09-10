namespace DataDictionary.Abstractions.Configuration;

/// <summary>
/// Controls how the library reacts when the diff engine detects a destructive or
/// ambiguous divergence between the manifest and the persisted dictionary (a
/// <c>Sync.BreakingChange</c>). Defaults to <see cref="Fail"/>, consistent with the
/// project's fail-fast, no-silent-write posture (Constitution Principle VII; see
/// <c>spec.md</c> <c>## Clarifications</c>).
/// </summary>
public enum OnBreakingChange
{
    /// <summary>
    /// The default: application boot aborts with an actionable error message
    /// describing every detected breaking change.
    /// </summary>
    Fail = 0,

    /// <summary>
    /// Application boot proceeds; every detected breaking change is logged as a
    /// warning and left unapplied.
    /// </summary>
    Warn,

    /// <summary>
    /// Application boot proceeds; every detected breaking change is silently left
    /// unapplied.
    /// </summary>
    Ignore,
}
