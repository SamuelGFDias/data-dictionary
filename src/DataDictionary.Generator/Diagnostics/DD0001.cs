using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Diagnostics;

/// <summary>
/// DD0001 (Error) — an enum member's code cannot be resolved by any configured source:
/// no explicit <c>[DictionaryValue]</c>, and either the enum is not convention-governed
/// (no fallback exists at all) or it is and the configured convention <c>CodeSource</c>
/// still yielded nothing. See <c>contracts/diagnostics-contract.md</c> (FR-025).
/// </summary>
internal static class DD0001
{
    internal const string Id = "DD0001";

    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: "Data dictionary member code could not be resolved",
        messageFormat: "Enum '{0}' member '{1}' has no resolvable data dictionary code: " +
            "add [DictionaryValue(\"...\")] to the member, or configure a convention " +
            "default (assembly: DataDictionaryDefaults/DataDictionaryScan) that resolves one",
        category: GeneratorConstants.DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every data dictionary member must resolve to a non-empty code, either explicitly via [DictionaryValue] or through an assembly-level convention default.");

    internal static Diagnostic Create(Location? location, string enumName, string memberName) =>
        Diagnostic.Create(Descriptor, location, enumName, memberName);
}
