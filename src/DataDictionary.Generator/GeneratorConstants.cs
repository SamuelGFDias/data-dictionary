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
    /// The default maximum length a resolved <c>code</c> may have before DD0003 fires
    /// (<c>contracts/diagnostics-contract.md</c>, FR-027) — the single source of truth
    /// for the value <c>64</c>, referenced by both
    /// <see cref="DataDictionary.Abstractions.DataDictionaryDefaultsAttribute.MaxCodeLength"/>'s
    /// own default and <see cref="Model.ConventionDefaults.Default"/>.
    /// </summary>
    /// <remarks>
    /// This is now a public configuration surface: an assembly can override it via
    /// <c>[assembly: DataDictionaryDefaults(MaxCodeLength = ...)]</c>, per the
    /// <c>2026-09-10</c> entry in <c>spec.md</c>'s <c>## Clarifications</c>. The override
    /// applies to the whole compilation (explicit mode and convention mode alike), since
    /// it is an assembly-level setting rather than a per-mode one — see
    /// <see cref="DictionaryModelBuilder"/>'s <c>ValidateMembers</c>. This constant
    /// remains the fallback used when no <c>[assembly: DataDictionaryDefaults]</c> is
    /// present in the compilation at all.
    /// </remarks>
    internal const int DefaultMaxCodeLength = 64;
}
