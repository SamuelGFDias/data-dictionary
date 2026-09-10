namespace DataDictionary.Abstractions.Configuration;

/// <summary>
/// Controls what the library does at application startup with the marked enums'
/// manifest against the persisted dictionary. Defaults to <see cref="Off"/>, so
/// adopting the library is inert until an environment is explicitly opted in (see
/// <c>spec.md</c> <c>## Clarifications</c>).
/// </summary>
public enum SyncMode
{
    /// <summary>
    /// No check and no write. The default: the library performs no synchronization
    /// work at all until a developer explicitly opts an environment into one of the
    /// other modes.
    /// </summary>
    Off = 0,

    /// <summary>
    /// Compares the manifest against the persisted dictionary and reports any
    /// divergence, but never writes.
    /// </summary>
    ValidateOnly,

    /// <summary>
    /// Writes the resolved differences (inserts, updates, deactivations) to the
    /// persisted dictionary, subject to the configured <c>OnBreakingChange</c>
    /// policy for anything destructive or ambiguous.
    /// </summary>
    Sync,

    /// <summary>Performs both the validation and the write in the same pass.</summary>
    SyncAndValidate,
}
