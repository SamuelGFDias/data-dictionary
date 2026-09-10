namespace DataDictionary.Generator.Diagnostics;

/// <summary>
/// DD0008 (Error, reserved) — detects that a member's resolved <c>code</c> changed
/// relative to a versioned baseline. This MVP ships no baseline lock file, so this
/// diagnostic ID is intentionally unused: no <see cref="Microsoft.CodeAnalysis.DiagnosticDescriptor"/>
/// exists for it, and it never fires. Reserving the constant now prevents a future
/// feature from accidentally reusing an ID already documented in
/// <c>contracts/diagnostics-contract.md</c>. See that contract for the full rationale.
/// </summary>
internal static class DD0008
{
    internal const string Id = "DD0008";
}
