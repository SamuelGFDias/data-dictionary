; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DD0001 | DataDictionary.Generator | Error | Member code could not be resolved by any configured source
DD0002 | DataDictionary.Generator | Error | Two members of the same enum resolve to the same code
DD0003 | DataDictionary.Generator | Error | A resolved code exceeds the configured maximum length
DD0004 | DataDictionary.Generator | Warning | Member has no resolvable description and RequireDescription is enabled
DD0005 | DataDictionary.Generator | Error | Enum key is empty, or two different enums share the same enum key
DD0006 | DataDictionary.Generator | Warning | Two members of the same enum share the same underlying numeric value
DD0007 | DataDictionary.Generator | Error | A [Flags] enum is marked as a data dictionary source
