namespace DataDictionary.Generator;

/// <summary>
/// Fixed constants shared across the generator's pipeline and diagnostics.
/// </summary>
internal static class GeneratorConstants
{
    /// <summary>
    /// The diagnostic category reported on every DDxxxx diagnostic.
    /// </summary>
    internal const string DiagnosticCategory = "DataDictionary.Generator";

    /// <summary>
    /// The maximum length a resolved <c>code</c> may have before DD0003 fires
    /// (<c>contracts/diagnostics-contract.md</c>, FR-027).
    /// </summary>
    /// <remarks>
    /// <c>data-model.md</c> describes this limit as "configurable, see Clarifications in
    /// <c>spec.md</c> for the still-open default value" — but as of this generator's
    /// implementation, <c>spec.md</c>'s <c>## Clarifications</c> section does not record
    /// a resolved default, and <c>DataDictionaryDefaultsAttribute</c> (the only
    /// convention-configuration surface currently defined in
    /// <c>DataDictionary.Abstractions</c>) has no property for it. Until a configuration
    /// surface for this value is added to the public contract, DD0003 enforces this
    /// fixed constant instead. This is a deliberate scope decision for this task, not a
    /// discovered bug — flagged for product-owner confirmation of the intended default
    /// and of where the eventual configuration surface should live.
    /// </remarks>
    internal const int DefaultMaxCodeLength = 64;
}
