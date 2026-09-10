using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator.Diagnostics;

/// <summary>
/// DD0004 (Warning) — a member has no resolvable description and
/// <c>[assembly: DataDictionaryDefaults(RequireDescription = true)]</c> is configured.
/// See <c>contracts/diagnostics-contract.md</c> (FR-028) and
/// <see cref="DataDictionary.Generator.DescriptionResolver"/>'s remarks for why this can
/// only fire for convention-governed (scanned) members.
/// </summary>
internal static class DD0004
{
    internal const string Id = "DD0004";

    internal static readonly DiagnosticDescriptor Descriptor = new(
        id: Id,
        title: "Data dictionary member has no resolvable description",
        messageFormat: "Enum '{0}' member '{1}' has no resolvable description (no XML doc <summary>, " +
            "[Description], or [Display(Name = ...)]) and RequireDescription is enabled; add one of these " +
            "or set RequireDescription = false",
        category: GeneratorConstants.DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When RequireDescription is enabled, a member without a real (non-member-name) description is flagged instead of silently falling back to its own C# name.");

    internal static Diagnostic Create(Location? location, string enumName, string memberName) =>
        Diagnostic.Create(Descriptor, location, enumName, memberName);
}
