namespace DataDictionary.Abstractions.Sync;

/// <summary>
/// Identifies which of the two divergences documented in <c>data-model.md</c>'s
/// <c>SynchronizationOutcome</c> section a given <see cref="BreakingChange"/>
/// represents.
/// </summary>
public enum BreakingChangeReason
{
    /// <summary>
    /// A stored, active row's <c>field_name</c> is absent from the manifest, and
    /// its code is still referenced by business data (FR-018) — the row cannot be
    /// deactivated without leaving a dangling reference.
    /// </summary>
    InUseCodeRemoved = 0,

    /// <summary>
    /// A member's resolved code changed in a way that collides with an existing,
    /// different <c>field_name</c>'s stored code (FR-019).
    /// </summary>
    CodeCollision,
}
