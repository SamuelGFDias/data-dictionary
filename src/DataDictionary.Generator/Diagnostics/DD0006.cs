using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Diagnostics;

/// <summary>
/// DD0006 (Warning) — two members of the same enum share the same underlying numeric
/// value (a numeric alias). Non-fatal: both members still appear in the emitted
/// manifest. See <c>contracts/diagnostics-contract.md</c> (FR-030).
/// </summary>
internal static class DD0006
{
    internal const string Id = "DD0006";

    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: "Data dictionary members share the same numeric value",
        messageFormat: "Enum '{0}' member '{1}' shares its underlying numeric value ({2}) with member " +
            "'{3}'; this is allowed (a numeric alias) but both are persisted as distinct dictionary rows",
        category: GeneratorConstants.DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Two enum members sharing an underlying numeric value is permitted but is surfaced as a warning since it usually signals an unintentional alias.");

    internal static Diagnostic Create(Location? location, string enumName, string memberName, long numericValue, string conflictingMemberName) =>
        Diagnostic.Create(Descriptor, location, enumName, memberName, numericValue, conflictingMemberName);
}
