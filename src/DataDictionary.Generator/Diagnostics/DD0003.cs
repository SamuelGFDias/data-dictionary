using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Diagnostics;

/// <summary>
/// DD0003 (Error) — a resolved <c>code</c> exceeds the configured maximum length.
/// Defaults to <see cref="GeneratorConstants.DefaultMaxCodeLength"/> (64), overridable
/// per assembly via
/// <c>DataDictionary.Abstractions.DataDictionaryDefaultsAttribute.MaxCodeLength</c>
/// — the override applies to every member in the compilation, explicit mode and
/// convention mode alike, since it is an assembly-level setting rather than a
/// convention-only one. See <c>contracts/diagnostics-contract.md</c> (FR-027) and the
/// <c>2026-09-10</c> entry in <c>spec.md</c>'s <c>## Clarifications</c>.
/// </summary>
internal static class DD0003
{
    internal const string Id = "DD0003";

    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: "Data dictionary code exceeds the maximum length",
        messageFormat: "Enum '{0}' member '{1}' resolves to code '{2}' ({3} characters), which " +
            "exceeds the maximum allowed length of {4} characters; shorten the code or change how it is resolved",
        category: GeneratorConstants.DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A dictionary code must fit within the configured maximum length so it fits the persisted column.");

    internal static Diagnostic Create(Location? location, string enumName, string memberName, string code, int maxLength) =>
        Diagnostic.Create(Descriptor, location, enumName, memberName, code, code.Length, maxLength);
}
