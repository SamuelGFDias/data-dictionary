# DataDictionary Constitution

## Core Principles

### I. Open Source, Strict SemVer
DataDictionary is developed in the open and published on NuGet. Every published package
version MUST follow Semantic Versioning (MAJOR.MINOR.PATCH) strictly. A breaking change
to any public contract (public API surface, generated code shape, persisted schema
contract, or configuration option) MUST be released as a MAJOR version bump only. No
breaking change may ship in a MINOR or PATCH release, regardless of urgency.

### II. Rigid Target-Framework Separation
`DataDictionary.Abstractions` and `DataDictionary.Generator` MUST target `netstandard2.0`
only. `DataDictionary.Core` and every provider package (e.g. EntityFrameworkCore) MUST
target `net10.0` only. This split is non-negotiable: it is what allows the generator to
run inside any Roslyn host (including older SDKs and IDEs) while the runtime libraries
use modern .NET features. A change to a project's target framework(s) is itself a
governance-level decision and MUST be reflected here before it is made.

### III. Core Has Zero ORM Dependencies
`DataDictionary.Core` MUST NEVER reference Entity Framework Core, Dapper, or any other
ORM or database-access library, directly or transitively. All persistence and querying
behavior is expressed exclusively through interfaces owned by `Core`. Providers (e.g.
`DataDictionary.EntityFrameworkCore`) implement those interfaces and own the ORM
dependency. This keeps `Core` reusable across any data-access technology and keeps
provider-specific concerns out of the shared engine.

### IV. Zero Hot-Path Reflection
The runtime code path exercised by a consuming application at steady state (after
startup synchronization) MUST NOT use reflection — no `Enum.Parse`, `Enum.GetValues`,
`Activator.CreateInstance`, or attribute inspection at request time. Anything that can be
resolved at compile time (enum-to-code mappings, descriptions, value converters, entity
configuration) MUST be produced by the source generator instead. Reflection is permitted
only in one-time startup/synchronization code paths that are not part of steady-state
request handling, and even there it MUST be minimized.

### V. AOT and Trimming Compatible
All shipped runtime assemblies (`Core` and providers) MUST be compatible with Native AOT
publishing and trimming. This follows directly from Principle IV: a codebase with no
hot-path reflection and compile-time-generated mappings is what makes AOT/trimming
compatibility achievable. Any API that cannot be made trim-safe MUST be annotated with
the appropriate trimming/AOT analyzer attributes so violations surface at the consumer's
build time, not at their runtime.

### VI. No Behavior Without a Test (NON-NEGOTIABLE)
Every new behavior is born with a test; there is no follow-up ticket to "add tests
later". Source-generator behavior MUST be covered by snapshot tests. Provider behavior
MUST be covered by integration tests that run against a real database via Testcontainers
— mocked or in-memory database substitutes do not satisfy this principle for provider
code. A pull request that adds behavior without a corresponding test MUST NOT be merged.

### VII. No Silent Writes in Production
The library MUST NEVER silently write over a destructive divergence between the enum
source of truth and the persisted dictionary. When a destructive or ambiguous change is
detected (e.g. a code still in use being removed, or a code collision), synchronization
MUST abort application boot with an actionable error message rather than proceeding with
a best-effort write. Silence in the face of destructive divergence is treated as a
correctness bug, not a convenience.

### VIII. The Consumer Is a Client of the Analyzer
Compile-time analyzers and the source generator MUST treat the consuming developer as a
client whose configuration mistakes deserve a clear, build-time diagnostic. Any
misconfiguration that can be detected statically (duplicate codes, unresolved
descriptions, invalid attribute usage, unsupported enum shapes) MUST surface as a build
error or warning with a stable diagnostic ID. It MUST NOT be deferred to a runtime
exception discovered only when the consumer's application boots or executes.

### IX. Bilingual, Fully Documented Public API
The README MUST document the library in both Portuguese and English. Every public type
and member MUST carry XML documentation comments. Undocumented public API is treated as
incomplete work, not as a follow-up task, because the generated diagnostics and the
public contract are the primary interface most consumers will read.

### X. Every Pending Item and Technical Debt Becomes an Issue
Every pending item or piece of technical debt identified while executing work MUST be
recorded as an issue in the repository — including when it was already resolved at the
moment it was found (e.g. a design decision made out of necessity, a configuration gap
filled with a provisional value, or an environment limitation worked around). This
applies even when the work itself was delivered successfully: the mere existence of a
non-trivial decision or debt is sufficient reason to open the issue, independent of
whether the surrounding task succeeded. The issue MUST document what was found, the
decision or workaround taken, and what remains pending validation or review. Silently
absorbing a workaround into "done" work, with no trace for a reviewer to follow up on,
is treated as incomplete work, not as acceptable pragmatism.

## Technology & Compatibility Constraints

- Target frameworks are fixed per Principle II and MUST NOT be loosened to "multi-target
  everything" for convenience.
- The solution builds with C# language features appropriate to each target framework's
  toolchain; language-version choices MUST NOT force `Core`/providers below `net10.0` or
  `Abstractions`/`Generator` above `netstandard2.0`.
- Generated code and public contracts MUST remain source-compatible with AOT publish and
  trimming as shipped; a regression here is treated as a breaking change under
  Principle I even if the public API text did not change.
- NuGet packaging MUST distribute the generator as a development-time-only dependency
  (analyzer asset) so it never becomes a runtime dependency of a consuming application.

## Quality Gates

- CI MUST run and pass: generator snapshot tests, Core unit tests, and provider
  integration tests (Testcontainers) before a change can merge.
- Any diagnostic ID introduced by the analyzer/generator MUST have at least one test
  proving it fires and at least one test proving it does not fire on valid input.
- A release MUST NOT be published from a build with failing tests, disabled test
  projects, or suppressed warnings introduced to force a pass.
- Documentation changes (README PT/EN, XML docs) MUST be reviewed as part of the same
  pull request that introduces or changes the public API they describe, not deferred.

## Governance

This constitution supersedes any conflicting practice, template default, or prior
informal convention in this repository. Every plan, specification, and task list
produced under Spec-Driven Development for this project MUST be checked against these
principles before implementation begins; any deviation MUST be justified explicitly in
that artifact's own documentation (e.g. a "Complexity Tracking" or equivalent section)
or the deviation MUST be rejected.

Amendment procedure: a change to this constitution is proposed as a pull request that
modifies this file directly, states the rationale for the change, and — if the change
affects Principles I through IX — is called out explicitly as a breaking governance
change. Amendments take effect on merge; `LAST_AMENDED_DATE` below MUST be updated to
the merge date.

Versioning policy for this document follows semantic versioning of the constitution
itself: MAJOR for backward-incompatible principle removal or redefinition, MINOR for a
new principle or materially expanded guidance, PATCH for clarifications and non-semantic
wording fixes.

Compliance review: every `speckit-plan` and `speckit-tasks` artifact for this project
MUST include or reference a constitution-compliance check. Reviewers MUST treat a
missing or unresolved compliance check as a blocking issue, not a nit.

**Version**: 1.1.0 | **Ratified**: 2026-09-10 | **Last Amended**: 2026-09-10
