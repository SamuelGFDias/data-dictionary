# Contract: compile-time diagnostics DD0001–DD0008

Every diagnostic ID below is part of the public contract (Constitution Principle VIII:
the consumer is treated as a client — configuration mistakes surface at build time). IDs,
severities, and the condition that fires them MUST NOT change without a MAJOR version
bump; wording of the message text may improve within a MINOR/PATCH release.

| ID | Severity | Fires when | Corresponds to |
|---|---|---|---|
| DD0001 | Error | An enum member's code cannot be resolved by any configured source (no explicit `[DictionaryValue]`, no XML doc, no `[Description]`/`[Display]`, and convention mode's `CodeSource` still yields nothing resolvable). | FR-025, spec Edge Cases |
| DD0002 | Error | Two members of the same enum resolve to the same `code`. | FR-026, spec acceptance scenario "duas entradas com o mesmo código" |
| DD0003 | Error | A resolved `code` exceeds the configured `MaxCodeLength`. | FR-027 |
| DD0004 | Warning | A member has no resolvable description and `RequireDescription=true`. | FR-028, spec Edge Cases |
| DD0005 | Error | Two different enums are marked with the same `enum_key`/dictionary key. | FR-029 |
| DD0006 | Warning | Two members of the same enum share the same underlying numeric value (alias). | FR-030, spec Edge Cases |
| DD0007 | Error | A `[Flags]` enum is marked as a dictionary source. | FR-031, spec Edge Cases |
| DD0008 | Error | A member's resolved `code` changed relative to a versioned baseline, for an otherwise-existing member. | Explicitly listed as a NON-OBJETIVO for this MVP (no baseline lock file ships); **this diagnostic ID is reserved but MUST NOT be implemented to fire in this feature** — see `plan.md` Complexity Tracking / Out of Scope note. |

## Note on DD0003's configured `MaxCodeLength`

`MaxCodeLength` defaults to 64 and is a public configuration surface —
`DataDictionaryDefaultsAttribute.MaxCodeLength` — not a fixed constant. The configured
value is an assembly-level setting: it applies to every member DD0003 validates in the
compilation, whether the owning enum is reached through explicit
`[DataDictionary]`/`[DictionaryValue]` attributes or through convention mode's
`[assembly: DataDictionaryScan]`. See the `2026-09-10` entry in `spec.md`'s
`## Clarifications`.

## MVP scope note on DD0008

The feature's own "Out of Scope (MVP)" section explicitly excludes a versioned baseline
lock file. DD0008 is reserved here (its ID is fixed now, in this feature's diagnostics
contract, so a later feature can implement it without an ID collision or renumbering) but
MUST NOT be wired to fire during this feature's implementation. Task generation
(`/speckit-tasks`) MUST NOT include a task that makes DD0008 fire; a task only for
reserving the ID in the shared diagnostics catalog is in scope.

## Message content requirement

Every diagnostic's message text MUST name the specific enum and member (and, where
applicable per FR-018's runtime analogue, the code) involved — consistent with the
project-wide rule (constitution + task's own restriction) that every fail-fast message
names the enum, the member, the code, and what to do next. A diagnostic that only says
"duplicate code" without naming the enum/members involved does not satisfy this contract.
