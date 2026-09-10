using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Diagnostics;

/// <summary>
/// DD0005 (Error) — either an enum's <c>enum_key</c> is empty/whitespace, or two
/// different enums resolve to the same <c>enum_key</c> within the compilation. See
/// <c>contracts/diagnostics-contract.md</c> (FR-029) and <c>data-model.md</c>'s
/// <c>EnumKey</c> validation rule ("must be non-empty and unique").
/// </summary>
internal static class DD0005
{
    internal const string Id = "DD0005";

    internal static readonly DiagnosticDescriptor EmptyKeyDescriptor = new(
        id: Id,
        title: "Data dictionary enum key is empty",
        messageFormat: "Enum '{0}' resolves to an empty or whitespace-only dictionary key; " +
            "enum_key must be a non-empty, unique identifier",
        category: GeneratorConstants.DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A data dictionary enum key must be non-empty; it is the stable identifier persisted alongside every member row.");

    internal static readonly DiagnosticDescriptor DuplicateKeyDescriptor = new(
        id: Id,
        title: "Duplicate data dictionary enum key",
        messageFormat: "Enum '{0}' resolves to dictionary key '{1}', which is already used by " +
            "enum '{2}'; every dictionary-eligible enum must have a unique enum_key across the compilation",
        category: GeneratorConstants.DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A data dictionary enum key must be unique across the whole compilation; it is the join key between the manifest and the persisted dictionary.");

    internal static Diagnostic CreateEmptyKey(Location? location, string enumName) =>
        Diagnostic.Create(EmptyKeyDescriptor, location, enumName);

    internal static Diagnostic CreateDuplicateKey(Location? location, string enumName, string enumKey, string conflictingEnumName) =>
        Diagnostic.Create(DuplicateKeyDescriptor, location, enumName, enumKey, conflictingEnumName);
}
