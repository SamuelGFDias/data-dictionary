using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Diagnostics;

/// <summary>
/// DD0003 (Error) — a resolved <c>code</c> exceeds the configured maximum length
/// (<see cref="GeneratorConstants.DefaultMaxCodeLength"/> — see that constant's remarks
/// for why it is fixed rather than sourced from a public configuration surface in this
/// implementation). See <c>contracts/diagnostics-contract.md</c> (FR-027).
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
