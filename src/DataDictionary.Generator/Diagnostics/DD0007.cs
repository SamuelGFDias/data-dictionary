using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Diagnostics;

/// <summary>
/// DD0007 (Error) — a <c>[Flags]</c> enum is marked as a dictionary source. Per
/// <c>data-model.md</c>'s <c>IsFlags</c> validation rule, such an enum MUST be excluded
/// from the emitted manifest entirely; this diagnostic fires instead. See
/// <c>contracts/diagnostics-contract.md</c> (FR-031).
/// </summary>
internal static class DD0007
{
    internal const string Id = "DD0007";

    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: "[Flags] enum cannot be a data dictionary source",
        messageFormat: "Enum '{0}' is decorated with [Flags] and cannot be a data dictionary source; " +
            "remove [DataDictionary]/[DataDictionaryScan] coverage from it, or remove [Flags] if it is not truly a bit-flag enum",
        category: GeneratorConstants.DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [Flags] enum's members are not mutually exclusive dictionary entries, so it cannot be synchronized as a data dictionary source.");

    internal static Diagnostic Create(Location? location, string enumName) =>
        Diagnostic.Create(Descriptor, location, enumName);
}
