namespace DataDictionary.Abstractions.Sync;

/// <summary>
/// One destructive or ambiguous divergence between the manifest and the persisted
/// dictionary, detected by the diff engine but never auto-applied — routed through
/// the configured <c>OnBreakingChange</c> policy instead (Constitution Principle
/// VII, "No Silent Writes in Production"). See <c>data-model.md</c>'s
/// <c>SynchronizationOutcome</c> section and <c>contracts/store-contract.md</c>.
/// </summary>
/// <param name="EnumKey">The affected enum's stable identifier.</param>
/// <param name="FieldName">The affected member's C# name.</param>
/// <param name="Code">The affected member's dictionary code.</param>
/// <param name="Reason">Which divergence this instance represents.</param>
/// <param name="ReferencingTables">
/// The identity of at least one table referencing <paramref name="Code"/>, as
/// obtained via <c>IsCodeInUseAsync</c>. Populated only when
/// <paramref name="Reason"/> is <see cref="BreakingChangeReason.InUseCodeRemoved"/>;
/// empty otherwise.
/// </param>
public sealed record BreakingChange(
    string EnumKey,
    string FieldName,
    string Code,
    BreakingChangeReason Reason,
    IReadOnlyList<string> ReferencingTables);
