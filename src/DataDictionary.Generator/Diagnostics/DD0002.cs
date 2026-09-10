using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Diagnostics;

/// <summary>
/// DD0002 (Error) — two members of the same enum resolve to the same <c>code</c>. See
/// <c>contracts/diagnostics-contract.md</c> (FR-026).
/// </summary>
internal static class DD0002
{
    internal const string Id = "DD0002";

    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: "Duplicate data dictionary code within an enum",
        messageFormat: "Enum '{0}' member '{1}' resolves to code '{2}', which is already used by " +
            "member '{3}' of the same enum; every member's code must be unique within its enum",
        category: GeneratorConstants.DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A dictionary code must uniquely identify one member within its owning enum.");

    internal static Diagnostic Create(Location? location, string enumName, string memberName, string code, string conflictingMemberName) =>
        Diagnostic.Create(Descriptor, location, enumName, memberName, code, conflictingMemberName);
}
