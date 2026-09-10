# Implementation Plan: Enum-Sourced Data Dictionary Sync

**Branch**: `001-enum-data-dictionary-sync` | **Date**: 2026-09-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-enum-data-dictionary-sync/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Build **DataDictionary**, a .NET library where a marked C# enum is the single source of
truth for the valid values of a database column. A Roslyn incremental source generator
reads enums marked with `[DataDictionary]`/convention attributes and emits, entirely at
compile time: a manifest of every dictionary-eligible enum and member, the EF Core entity
mapping for a generic dictionary table pair, and the `ValueConverter`s wiring business
entities' enum properties to their persisted codes. At application startup, `Core`
compares that compiled manifest against the live database through a
provider-implemented `IDataDictionaryStore`, applies non-destructive changes
automatically, and aborts boot with an actionable message on any destructive divergence.
The technical approach (detailed in `research.md`) rests on nine resolved decisions:
strict `netstandard2.0`/`net10.0` target-framework separation, EF Core as the sole
MVP provider (SQL Server + PostgreSQL), `ForAttributeWithMetadataName` with fully
equatable pipeline models for generator incrementality, a generated static manifest as
the zero-reflection bridge from compile time to runtime, standard analyzer-package NuGet
shaping, native per-database advisory locks for concurrent-boot safety, EF model
inspection for automatic in-use detection, a runtime-seeder-by-default / `HasData`-
opt-in seed strategy enabled by the natural composite primary key, and a
Verify+Testcontainers-based test stack matching the constitution's testing mandate.

## Technical Context

**Language/Version**: C# 14 on .NET 10 for `Core` and providers; `Abstractions` and
`Generator` compile against the `netstandard2.0` API surface (language version pinned to
whatever the solution's SDK resolves for a `netstandard2.0` target — no `net10.0`-only
language features are used in those two projects).

**Primary Dependencies**: `Microsoft.CodeAnalysis.CSharp` (Roslyn, `PrivateAssets="all"`)
for `Generator`; `Microsoft.EntityFrameworkCore` (+ SQL Server and Npgsql providers) for
`DataDictionary.EntityFrameworkCore`; no runtime dependency beyond the BCL for `Core` or
`Abstractions`.

**Storage**: Relational, via the EF Core provider — SQL Server and PostgreSQL for the
MVP (both named explicitly by the distributed-lock requirement; see `research.md` §2 and
§6). No other storage technology is in scope.

**Testing**: xUnit throughout. `DataDictionary.Generator.Tests` adds `Verify` +
`Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` for snapshot testing of
generator output/diagnostics. `DataDictionary.EntityFrameworkCore.Tests` adds
`Testcontainers.MsSql` / `Testcontainers.PostgreSql` for real-database integration tests.
`DataDictionary.Core.Tests` stays provider-free (diff engine unit tests only). See
`research.md` §9.

**Target Platform**: Cross-platform .NET 10 server/library targets (Linux and Windows);
distributed as NuGet packages, not as a hosted service.

**Project Type**: Library (multi-package: source generator + analyzers, a
provider-agnostic core, one EF Core provider) with an accompanying sample application
demonstrating the end-to-end flow. No frontend/mobile component.

**Performance Goals**: No steady-state (request-handling) cost at all — Constitution
Principle IV means the generated `ValueConverter`s and manifest lookups the hot path uses
are O(1) compiled data, not reflection. Synchronization cost is paid once at startup per
process/replica and is not benchmarked against a request-latency budget; its budget is
"acceptable added boot time for the number of marked enums/members a typical consuming
application has" (tens to low hundreds of entries), validated functionally rather than
via a numeric SLA in this feature.

**Constraints**: Every shipped runtime assembly (`Core`, `EntityFrameworkCore`) must
remain Native-AOT- and trimming-compatible (Principle V). No `Enum.Parse`,
`Enum.GetValues`, or other reflection may appear in the code path a consuming
application's own runtime executes (generator-internal Roslyn reflection over syntax
trees is unrelated and unrestricted). Every fail-fast/error message must name the enum,
the member, the code, and what to do next. The solution builds with nullable reference
types enabled and warnings-as-errors across every project.

**Scale/Scope**: This feature covers the full MVP surface described in `spec.md`
(explicit + convention marking, generic dictionary table + optional catalog table,
four sync modes, fail-fast diff rules, distributed lock, DD0001–DD0007 diagnostics,
DD0008 reserved-but-inert). Dapper provider, CLI, SQL/CSV export, and the DD0008 baseline
lock file are explicitly out of scope (spec.md "Out of Scope (MVP)") and this plan does
not design their implementation, only confirms nothing here blocks adding them later
(see `research.md` §1–§2, and `contracts/store-contract.md`'s "why Core never references
an ORM" note).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Initial check (pre-research) | Post-design check (after Phase 1) |
|---|---|---|
| I. Open source, strict SemVer | Package/README/versioning are implementation-phase concerns; this plan's contracts (`contracts/*.md`) are written as the frozen-until-MAJOR public surface, satisfying the gate at design time. | PASS — every contract file carries an explicit "breaking change = MAJOR" compatibility note. |
| II. Rigid TFM separation | Project Structure below enumerates exactly the four `src/` projects with exactly the TFMs mandated. | PASS — no project in the structure multi-targets across the boundary; `research.md` §1 documents why. |
| III. Core never references an ORM | `contracts/store-contract.md` defines `IDataDictionaryStore` entirely in `Abstractions`-owned types. | PASS — the contract's own "Why Core never references an ORM" section is the design-time proof; `Core`'s planned dependency list (Technical Context) has zero ORM packages. |
| IV. Zero hot-path reflection | `research.md` §4 designs the generated-manifest bridge specifically to avoid runtime reflection. | PASS — `data-model.md`'s compile-time models and the generated-entrypoints contract confirm the manifest is compiled data, not reflected data. |
| V. AOT/trimming compatible | Follows directly from IV; no design element here requires runtime `Type`/reflection APIs. | PASS — nothing in `data-model.md` or the contracts introduces a trim-unsafe pattern (no `Activator.CreateInstance`, no non-generic reflection-based (de)serialization). |
| VI. No behavior without a test | `research.md` §9 fixes the exact test stack per project, matching the constitution's own wording. | PASS — `quickstart.md` maps every scenario to a specific test project/class. |
| VII. No silent writes | `data-model.md`'s `SynchronizationOutcome.BreakingChanges` is structurally separated from `ToInsert`/`ToUpdate`/`ToDeactivate` and is never auto-applied by `ApplyAsync`. | PASS — the store contract's doc comment on `ApplyAsync` states this explicitly. |
| VIII. Consumer treated as a client (build-time errors) | `contracts/diagnostics-contract.md` fixes DD0001–DD0008 (DD0008 reserved, inert this feature) with required message content. | PASS. |
| IX. Bilingual docs + XML doc | Not a design-phase artifact by itself, but every contract file above documents the exact public members that will require XML doc + README PT/EN coverage during implementation; `quickstart.md` Scenario A is the literal README example. | PASS — tracked as an implementation-phase requirement, not a design gap. |

No violations requiring justification were found at either checkpoint — the
**Complexity Tracking** table below is intentionally empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-enum-data-dictionary-sync/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
│   ├── store-contract.md
│   ├── attributes-contract.md
│   ├── generated-entrypoints-contract.md
│   └── diagnostics-contract.md
├── checklists/
│   └── requirements.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

This is a **library** project (not a web/mobile app), and the task authorizing this plan
fixes the exact structure below verbatim. No files under `src/`, `tests/`, or `samples/`
are created by this planning phase — the tree is documented here as the target for the
implementation phase, which begins only after separate human approval.

```text
src/
├── DataDictionary.Abstractions/          # netstandard2.0 — attributes, IDataDictionaryStore
│                                          #   and every other cross-boundary contract type
├── DataDictionary.Generator/             # netstandard2.0 — IIncrementalGenerator + analyzers
│                                          #   (DD0001–DD0008), packed as analyzers/dotnet/cs
├── DataDictionary.Core/                  # net10.0 — diff engine, sync orchestration, policy
│                                          #   engine; zero ORM dependency (Principle III)
└── DataDictionary.EntityFrameworkCore/   # net10.0 — IDataDictionaryStore impl, ApplyDataDictionary(),
                                           #   ValueConverters, SQL Server + PostgreSQL lock impls

tests/
├── DataDictionary.Generator.Tests/               # snapshot (Verify + SourceGenerators.Testing)
├── DataDictionary.Core.Tests/                    # unit, focused on the diff engine
└── DataDictionary.EntityFrameworkCore.Tests/     # integration (Testcontainers: MsSql, PostgreSql)

samples/
└── Sample.Api/                            # minimal app demonstrating the full flow (quickstart.md)

Directory.Build.props                      # shared project settings (nullable, warnings-as-errors,
Directory.Packages.props                   #   LangVersion) + central package management

.github/
└── workflows/
    ├── ci.yml                             # build + test (all four TFmd-split projects) on PR/push
    └── release.yml                        # pack + publish to NuGet on tag/release
```

**Structure Decision**: Single multi-project library solution (no web/mobile split
applies). The `src/` split is the direct, non-negotiable expression of Constitution
Principle II (TFM separation) and Principle III (Core has no ORM dependency) — each
principle maps to a project boundary, not merely a folder convention, so that the
build system itself enforces the separation (a `netstandard2.0` project physically
cannot reference a `net10.0`-only API, and `Core`'s `.csproj` simply never lists an EF
Core `PackageReference`). `samples/Sample.Api` exists solely to make `quickstart.md`'s
scenarios runnable; it is not published.

## Implementation Slices

Ordered so that after every slice, the solution **compiles and its existing tests are
green** — no slice leaves the tree in a partially-working state for longer than the
slice itself takes to implement. `/speckit-tasks` breaks each slice into individually
testable tasks; this is the slice-level sequencing those tasks are drawn from.

1. **Abstractions skeleton** — `DataDictionary.Abstractions` (netstandard2.0): the
   attributes (`contracts/attributes-contract.md`), `IDataDictionaryStore` and its
   supporting POCOs (`contracts/store-contract.md`), and the manifest/model types shared
   with the generator (`data-model.md`). No behavior yet, just the contract. Compiles
   standalone; no tests to fail yet beyond "it compiles."
2. **Generator: happy path + DD0001/DD0002/DD0007** — `DataDictionary.Generator`
   (netstandard2.0): incremental pipeline on `ForAttributeWithMetadataName`, equatable
   models (`research.md` §3), emitting `DataDictionaryManifest.Default` for the simplest
   valid case (explicit attributes, no convention mode yet), plus the three diagnostics
   most directly tied to correctness (unresolvable code, duplicate code, `[Flags]`
   rejection). Green: `DataDictionary.Generator.Tests` snapshot tests for these cases.
3. **Generator: convention mode + remaining diagnostics** — `DataDictionaryDefaults` /
   `DataDictionaryScan`, description precedence (FR-005), and DD0003/DD0004/DD0005/DD0006.
   Green: expanded `Generator.Tests` snapshots; DD0008 reserved as an unused ID constant
   only (see `contracts/diagnostics-contract.md`).
4. **Core: diff engine** — `DataDictionary.Core` (net10.0): pure, provider-free
   classification logic building `SynchronizationOutcome` from a manifest and a
   `CurrentDictionaryState` + `IsCodeInUseAsync` results (both fed as test doubles at
   this stage — no real `IDataDictionaryStore` implementation exists yet). Green:
   `DataDictionary.Core.Tests` covering insert/update/deactivate/breaking-change
   classification and the four sync modes' orchestration logic.
5. **EF Core provider: mapping + manifest wiring, no lock yet** —
   `DataDictionary.EntityFrameworkCore` (net10.0): `ApplyDataDictionary()`, entity
   mappings for both tables with configurable naming (FR-011), generated
   `ValueConverter`s, and a real `IDataDictionaryStore.GetCurrentAsync`/`ApplyAsync`/
   `IsCodeInUseAsync` implementation against a real database. Green: first
   `EntityFrameworkCore.Tests` integration tests — Scenarios A–D from `quickstart.md`,
   single-instance only.
6. **EF Core provider: distributed lock** — `AcquireLockAsync` for SQL Server
   (`sp_getapplock`) and PostgreSQL (`pg_advisory_lock`), replica fallback to
   `ValidateOnly` (FR-024). Green: Scenario F (concurrent boot) added to
   `EntityFrameworkCore.Tests`.
7. **Seed strategy option** — `SeedStrategy.Migration` via `HasData`, built on the
   natural key already in place since Slice 1/5 (`research.md` §8). Green: a focused
   integration test proving `HasData`-seeded rows satisfy the same diff engine
   expectations as runtime-seeded ones.
8. **Sample + quickstart wiring** — `samples/Sample.Api` implementing the literal
   `RacaCor` example from `quickstart.md` Scenario A, proving every generated entry point
   in `contracts/generated-entrypoints-contract.md` is callable exactly as documented.
   Green: all `quickstart.md` scenarios pass when run against the sample.
9. **Packaging + CI/CD** — `Directory.Build.props`/`Directory.Packages.props` finalized
   (nullable + warnings-as-errors + central package management), NuGet packaging per
   `research.md` §5 (`analyzers/dotnet/cs`, `DevelopmentDependency`, `PrivateAssets="all"`
   on Roslyn refs), `.github/workflows/ci.yml` (build + full test suite including
   Testcontainers) and `release.yml` (pack + publish on tag). Green: CI passes on the
   full solution end-to-end.
10. **Documentation** — README (PT/EN) with the copyable `RacaCor` example
    (Constitution Principle IX), XML doc audit across every public member introduced in
    Slices 1–7 (build treats missing XML doc as a warning-as-error, so this slice's own
    test is "the build is clean").

Slices 1–4 have no database dependency and can be implemented and reviewed in parallel by
different people if desired; Slices 5–7 are strictly sequential (each depends on the
previous provider capability); 8–10 depend on everything before them.

## Complexity Tracking

*No entries — the Constitution Check above found no violations requiring
justification.*
