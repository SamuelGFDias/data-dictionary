---

description: "Task list template for feature implementation"
---

# Tasks: Enum-Sourced Data Dictionary Sync

**Input**: Design documents from `/specs/001-enum-data-dictionary-sync/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
(all present)

**Tests**: Included and REQUIRED for every phase — Constitution Principle VI ("Todo
comportamento novo nasce com teste") mandates a test with every new behavior, and the
feature owner explicitly requires each task to leave the solution green. Generator tasks
pair with snapshot tests (Verify + SourceGenerators.Testing); Core tasks pair with unit
tests; EF Core provider tasks pair with Testcontainers integration tests — per
`research.md` §9.

**Organization**: Tasks are grouped by user story (priority order from `spec.md`) so each
story can be implemented and tested independently, after the shared foundation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Every task includes its exact file path

## Path Conventions

Library solution per `plan.md` Project Structure — `src/`, `tests/`, `samples/` at
repository root, exactly as documented there. No file under `src/`, `tests/`, or
`samples/` exists yet; every task below is a to-do for the implementation phase (not
executed by `/speckit-tasks` itself).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution and project scaffolding, matching `plan.md`'s Project Structure
exactly (Constitution Principle II: the TFM split is enforced by project boundaries).

- [X] T001 Create `DataDictionary.sln` at the repository root, referencing every project
      listed in T002–T009.
- [X] T002 [P] Scaffold `src/DataDictionary.Abstractions/DataDictionary.Abstractions.csproj`
      targeting `netstandard2.0` only.
- [X] T003 [P] Scaffold `src/DataDictionary.Generator/DataDictionary.Generator.csproj`
      targeting `netstandard2.0` only, referencing `Microsoft.CodeAnalysis.CSharp` with
      `PrivateAssets="all"` (research.md §5).
- [X] T004 [P] Scaffold `src/DataDictionary.Core/DataDictionary.Core.csproj` targeting
      `net10.0` only, referencing `DataDictionary.Abstractions` — no EF Core, Dapper, or
      any ORM package reference (Constitution Principle III).
- [X] T005 [P] Scaffold
      `src/DataDictionary.EntityFrameworkCore/DataDictionary.EntityFrameworkCore.csproj`
      targeting `net10.0` only, referencing `DataDictionary.Core`,
      `Microsoft.EntityFrameworkCore`, the SQL Server EF Core provider, and the
      PostgreSQL (Npgsql) EF Core provider.
- [X] T006 [P] Scaffold
      `tests/DataDictionary.Generator.Tests/DataDictionary.Generator.Tests.csproj`
      referencing xUnit, `Verify.Xunit`, and
      `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` (research.md §9).
- [X] T007 [P] Scaffold `tests/DataDictionary.Core.Tests/DataDictionary.Core.Tests.csproj`
      referencing xUnit and `DataDictionary.Core` only (no database dependency).
- [X] T008 [P] Scaffold
      `tests/DataDictionary.EntityFrameworkCore.Tests/DataDictionary.EntityFrameworkCore.Tests.csproj`
      referencing xUnit, `Testcontainers.MsSql`, and `Testcontainers.PostgreSql`.
- [X] T009 [P] Scaffold `samples/Sample.Api/Sample.Api.csproj` (minimal `net10.0` web app)
      referencing `DataDictionary.EntityFrameworkCore`.
- [X] T010 Create `Directory.Build.props` at the repository root enabling
      `Nullable=enable` and `TreatWarningsAsErrors=true` for every project in the
      solution (plan.md Technical Context — "nullable enabled, warnings as errors na
      solução").
- [X] T011 [P] Create `Directory.Packages.props` at the repository root enabling central
      package management (`ManagePackageVersionsCentrally=true`) and pinning every
      dependency version named across T002–T009.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared contract types, the generator's compile-time pipeline, and the base
EF Core mapping — everything every user story's tests depend on before any story-specific
sync behavior exists.

**⚠️ CRITICAL**: No user story work (Phase 3+) can begin until this phase is complete.

### Abstractions (public contract types)

- [X] T012 [P] Define `DataDictionaryAttribute` (constructor `(string enumKey)`, `Group`
      named property) and `DictionaryValueAttribute` (constructor `(string code)`,
      `Deprecated` named property, default `false`) in
      `src/DataDictionary.Abstractions/Attributes/DataDictionaryAttribute.cs` and
      `DictionaryValueAttribute.cs`, exactly per
      `specs/001-enum-data-dictionary-sync/contracts/attributes-contract.md`.
- [X] T013 [P] Define assembly-level `DataDictionaryDefaultsAttribute` (`CodeSource`,
      `DescriptionFrom`, `RequireDescription` named properties) and repeatable
      `DataDictionaryScanAttribute(string namespacePrefix)` in
      `src/DataDictionary.Abstractions/Attributes/DataDictionaryDefaultsAttribute.cs` and
      `DataDictionaryScanAttribute.cs`, per `contracts/attributes-contract.md`.
- [X] T014 [P] Define `CodeSource` and `DescriptionSource` enums in
      `src/DataDictionary.Abstractions/CodeSource.cs` and `DescriptionSource.cs`, with
      `DescriptionSource` ordered `XmlDoc, DescriptionAttribute, DisplayAttribute,
      MemberName` matching the precedence in `spec.md` FR-005.
- [X] T015 [P] Define the compile-time model types `EnumDictionaryModel` and
      `DictionaryMemberModel` as immutable, structurally-equatable records — every
      collection field is `ImmutableArray<T>` paired with an explicit equality comparer,
      never default reference equality — in
      `src/DataDictionary.Abstractions/Manifest/EnumDictionaryModel.cs` and
      `DictionaryMemberModel.cs`, per `data-model.md` "Compile-time models" and
      `research.md` §3 (this is required to keep the Roslyn incremental cache warm).
- [X] T016 [P] Define `DataDictionaryManifest` and `ManifestEnumEntry` in
      `src/DataDictionary.Abstractions/Manifest/DataDictionaryManifest.cs`, per
      `data-model.md`'s `DataDictionaryManifest` section.
- [X] T017 [P] Define `DictionaryEntry` with exactly the columns `enum_key, field_name,
      code, numeric_value, description, group_name, is_active, is_deprecated,
      sort_order, content_hash, created_at, updated_at` (data-model.md `DictionaryEntry`
      table) and `DictionaryEnumCatalogEntry` with exactly `enum_key, clr_full_name,
      assembly_name, description, group_name, manifest_hash, last_sync_at`
      (data-model.md `DictionaryEnumCatalogEntry` table) in
      `src/DataDictionary.Abstractions/Persistence/DictionaryEntry.cs` and
      `DictionaryEnumCatalogEntry.cs`.
- [X] T018 [P] Define `SynchronizationOutcome` (fields `ToInsert, ToUpdate,
      ToDeactivate, BreakingChanges, Unchanged` per `data-model.md`
      `SynchronizationOutcome` table), `BreakingChange`, `CurrentDictionaryState`,
      `CodeUsageResult` (`bool InUse` + `IReadOnlyList<string> ReferencingTables`), and
      `LockAcquisitionResult` in `src/DataDictionary.Abstractions/Sync/` (one file per
      type), per `data-model.md` and `contracts/store-contract.md`'s "Supporting types".
- [X] T019 Define `IDataDictionaryStore` with `GetCurrentAsync`, `ApplyAsync`,
      `IsCodeInUseAsync`, `AcquireLockAsync` — exact signatures per
      `contracts/store-contract.md` — in
      `src/DataDictionary.Abstractions/IDataDictionaryStore.cs`. (depends on T017, T018)
- [X] T020 [P] Define `SyncMode` (`Off, ValidateOnly, Sync, SyncAndValidate`; default
      `Off` per `spec.md` `## Clarifications`) and `OnBreakingChange` (`Fail, Warn,
      Ignore`; default `Fail` per `spec.md` `## Clarifications`) enums in
      `src/DataDictionary.Abstractions/Configuration/SyncMode.cs` and
      `OnBreakingChange.cs`.
- [X] T021 [P] Define naming-convention configuration
      (`DataDictionaryNamingOptions`: table names defaulting to `tb_dicionario_dados` /
      `tb_dicionario_enum`, schema, column-naming convention defaulting to snake_case —
      all per `spec.md` `## Clarifications` and FR-011) in
      `src/DataDictionary.Abstractions/Configuration/DataDictionaryNamingOptions.cs`.

### Generator (compile-time pipeline + diagnostics)

- [ ] T022 Implement the `IIncrementalGenerator` registration using
      `ForAttributeWithMetadataName` for `[DataDictionary]`/`[DictionaryValue]`
      (explicit mode), producing the equatable pipeline models from T015, in
      `src/DataDictionary.Generator/DataDictionaryIncrementalGenerator.cs`, per
      `research.md` §3. (depends on T012, T014, T015)
- [ ] T023 Implement description-precedence resolution (XML doc `<summary>` →
      `[Description]` → `[Display(Name=)]` → member name, FR-005) in
      `src/DataDictionary.Generator/DescriptionResolver.cs`. (depends on T022)
- [ ] T024 Implement convention-mode scanning driven by
      `[assembly: DataDictionaryDefaults]` / `[assembly: DataDictionaryScan]` (FR-003,
      FR-004) in `src/DataDictionary.Generator/ConventionModeScanner.cs`. (depends on
      T013, T022, T023)
- [ ] T025 [P] [Snapshot test] Add generator snapshot tests for the explicit-mode happy
      path (single enum, single member, each of the four description sources) in
      `tests/DataDictionary.Generator.Tests/HappyPathTests.cs` (Verify +
      SourceGenerators.Testing, research.md §9). Write first so it FAILS before T022/T023
      exist, then confirm green.
- [ ] T026 [P] [Snapshot test] Add generator snapshot tests for convention mode
      (assembly-level defaults + scan, including the `RequireDescription=true` case) in
      `tests/DataDictionary.Generator.Tests/ConventionModeTests.cs`. (depends on T024)
- [ ] T027 Implement diagnostic **DD0001** (Error — member code unresolvable by any
      configured source) naming the enum and member, in
      `src/DataDictionary.Generator/Diagnostics/DD0001.cs`, per
      `contracts/diagnostics-contract.md`. (depends on T023)
- [ ] T028 [P] Implement diagnostic **DD0002** (Error — duplicate code within the same
      enum) naming the enum and both colliding members, in
      `src/DataDictionary.Generator/Diagnostics/DD0002.cs`.
- [ ] T029 [P] Implement diagnostic **DD0003** (Error — code exceeds
      `MaxCodeLength`) in `src/DataDictionary.Generator/Diagnostics/DD0003.cs`.
- [ ] T030 [P] Implement diagnostic **DD0004** (Warning — no resolvable description with
      `RequireDescription=true`) in `src/DataDictionary.Generator/Diagnostics/DD0004.cs`.
      (depends on T024)
- [ ] T031 [P] Implement diagnostic **DD0005** (Error — duplicate `enum_key` across two
      enums) in `src/DataDictionary.Generator/Diagnostics/DD0005.cs`.
- [ ] T032 [P] Implement diagnostic **DD0006** (Warning — two members share the same
      underlying numeric value) in `src/DataDictionary.Generator/Diagnostics/DD0006.cs`.
- [ ] T033 Implement diagnostic **DD0007** (Error — `[Flags]` enum marked as a
      dictionary source; the enum MUST be excluded from the emitted manifest per
      `data-model.md`'s `IsFlags` validation rule) in
      `src/DataDictionary.Generator/Diagnostics/DD0007.cs`. (depends on T022)
- [ ] T034 [P] [Snapshot test] Add one snapshot test per diagnostic DD0001–DD0007 (a
      positive case where it fires, and a negative case on otherwise-valid input where it
      does not) in `tests/DataDictionary.Generator.Tests/DiagnosticsTests.cs`, asserting
      each message names the enum/member(s) per `contracts/diagnostics-contract.md`'s
      message-content requirement. (depends on T027-T033)
- [ ] T035 Emit the generated `DataDictionaryManifest.Default` static entry point (per
      `contracts/generated-entrypoints-contract.md`) built entirely from compile-time
      literal data — no reflection — in
      `src/DataDictionary.Generator/ManifestEmitter.cs`. (depends on T022, T023, T024,
      T033)

### Core (DI/config skeleton — diff engine itself is story-specific)

- [ ] T036 Implement `AddDataDictionary(Action<IDataDictionaryBuilder> configure)` and
      `IDataDictionaryBuilder.AddManifest(DataDictionaryManifest manifest)` in
      `src/DataDictionary.Core/DependencyInjection/DataDictionaryServiceCollectionExtensions.cs`,
      per `contracts/generated-entrypoints-contract.md`. (depends on T016, T020)
- [ ] T037 Implement `DataDictionaryOptions` resolving the configured `SyncMode`
      (default `Off`) and `OnBreakingChange` policy (default `Fail`) per `spec.md`
      `## Clarifications`, in `src/DataDictionary.Core/DataDictionaryOptions.cs`.
      (depends on T020)

### EF Core provider (base mapping only)

- [ ] T038 Implement `ModelBuilder.ApplyDataDictionary()` configuring the
      `tb_dicionario_dados` / `tb_dicionario_enum` entity mappings — default snake_case
      columns, default table names, configurable per T021 — including the filtered
      unique index on `(enum_key, code)` for `is_active = 1`, in
      `src/DataDictionary.EntityFrameworkCore/ModelBuilderExtensions.cs`, per
      `contracts/generated-entrypoints-contract.md` and `data-model.md`. (depends on
      T017, T021)
- [ ] T039 Implement the generated `ValueConverter` wiring applied to every business
      entity property whose CLR type is a marked enum (FR-012) in
      `src/DataDictionary.EntityFrameworkCore/EnumCodeValueConverter.cs`. (depends on
      T016, T038)
- [ ] T040 [P] [Integration test] Add a Testcontainers-backed test (SQL Server and
      PostgreSQL) asserting `ApplyDataDictionary()` creates the expected tables,
      columns, and the filtered unique index, in
      `tests/DataDictionary.EntityFrameworkCore.Tests/SchemaMappingTests.cs`, per
      `research.md` §9. (depends on T038)

**Checkpoint**: Foundation ready — every user story phase below can now begin.

---

## Phase 3: User Story 1 - First sync populates the dictionary (Priority: P1) 🎯 MVP

**Goal**: Mark one enum, start the app against an empty database in `Sync` mode, and see
the dictionary table populated automatically — one row per member, correct code, numeric
value, and description.

**Independent Test**: Run `quickstart.md` Scenario A end-to-end against
`samples/Sample.Api` and query the dictionary table directly.

### Tests for User Story 1 ⚠️

> Write these tests FIRST; confirm they FAIL before the implementation tasks below.

- [ ] T041 [P] [US1] Add a Core unit test for the diff engine's insert-only
      classification (empty `CurrentDictionaryState`, full manifest → every member in
      `ToInsert`; nothing in `ToUpdate`/`ToDeactivate`/`BreakingChanges`) in
      `tests/DataDictionary.Core.Tests/DiffEngineInsertTests.cs`.
- [ ] T042 [P] [US1] Add an EF Core integration test (SQL Server and PostgreSQL) for
      `quickstart.md` Scenario A — empty database, `RacaCor` enum with `Branca = 1` /
      code `B`, `Sync` mode, asserting the exact resulting row `RacaCor | Branca | B | 1
      | Branca` — in
      `tests/DataDictionary.EntityFrameworkCore.Tests/FreshSyncTests.cs`.

### Implementation for User Story 1

- [ ] T043 [US1] Implement the diff engine's insert classification (`ToInsert`
      population by comparing manifest field names against `CurrentDictionaryState`) in
      `src/DataDictionary.Core/Sync/DictionaryDiffEngine.cs`. (depends on T018, T041)
- [ ] T044 [US1] Implement `IDataDictionaryStore.GetCurrentAsync` (EF Core), reading
      existing `DictionaryEntry` / `DictionaryEnumCatalogEntry` rows for one `enum_key`,
      in `src/DataDictionary.EntityFrameworkCore/EfDataDictionaryStore.cs`. (depends on
      T038)
- [ ] T045 [US1] Implement `IDataDictionaryStore.ApplyAsync` (EF Core) for the
      insert-only case — atomic insert of every `ToInsert` entry per enum — in
      `src/DataDictionary.EntityFrameworkCore/EfDataDictionaryStore.cs`. (depends on
      T044)
- [ ] T046 [US1] Implement the startup synchronization orchestrator's `Sync`-mode path
      (compute the outcome via the diff engine, then call `ApplyAsync`) in
      `src/DataDictionary.Core/Sync/DataDictionarySynchronizer.cs`. (depends on T036,
      T037, T043, T045)
- [ ] T047 [US1] Wire `samples/Sample.Api` with the `RacaCor` enum, the generated
      `AddDataDictionary`/`AddManifest`/`ApplyDataDictionary()` calls, and `SyncMode.Sync`
      configured explicitly, per `quickstart.md` Scenario A, in
      `samples/Sample.Api/Program.cs` and `samples/Sample.Api/Enums/RacaCor.cs`.
      (depends on T035, T046)

**Checkpoint**: User Story 1 is fully functional and independently testable —
T041/T042 pass.

---

## Phase 4: User Story 2 - Destructive divergence aborts the boot (Priority: P1)

**Goal**: A member removed from an enum whose code is still referenced by business data
must block startup with a message naming the enum, member, code, and referencing table —
never silently strand or mislabel data.

**Independent Test**: Run `quickstart.md` Scenarios C and E against
`samples/Sample.Api`; confirm the boot fails with the required message content and no
write occurs.

### Tests for User Story 2 ⚠️

- [ ] T048 [P] [US2] Add a Core unit test classifying a removed-but-in-use member into
      `BreakingChanges` (never `ToDeactivate`) in
      `tests/DataDictionary.Core.Tests/DiffEngineBreakingChangeTests.cs`.
- [ ] T049 [P] [US2] Add a Core unit test classifying a code-collision (an existing
      `field_name`'s code changed to collide with another entry) into
      `BreakingChanges`, never auto-applied, in
      `tests/DataDictionary.Core.Tests/DiffEngineCollisionTests.cs`.
- [ ] T050 [P] [US2] Add an EF Core integration test for `quickstart.md` Scenario C
      (in-use code removed → boot fails naming enum, member, code, and the referencing
      business table) in
      `tests/DataDictionary.EntityFrameworkCore.Tests/BreakingChangeTests.cs`.
- [ ] T051 [P] [US2] Add an EF Core integration test for `quickstart.md` Scenario E
      (`ValidateOnly` + divergence → boot fails, database left byte-for-byte unchanged)
      in `tests/DataDictionary.EntityFrameworkCore.Tests/ValidateOnlyTests.cs`.

### Implementation for User Story 2

- [ ] T052 [US2] Implement `IDataDictionaryStore.IsCodeInUseAsync` (EF Core) by walking
      `DbContext.Model` for properties typed as the marked enum and querying for the
      retired numeric/code value, returning the referencing table name(s), per
      `research.md` §7, in
      `src/DataDictionary.EntityFrameworkCore/EfDataDictionaryStore.cs`. (depends on
      T044)
- [ ] T053 [US2] Extend the diff engine with breaking-change classification (removed-
      and-in-use; code collision) populating `SynchronizationOutcome.BreakingChanges` in
      `src/DataDictionary.Core/Sync/DictionaryDiffEngine.cs`. (depends on T043, T052)
- [ ] T054 [US2] Implement the `OnBreakingChange` policy engine (`Fail` aborts boot
      before any write; `Warn` logs and continues; `Ignore` silently skips the breaking
      entries; default `Fail`) in
      `src/DataDictionary.Core/Sync/BreakingChangePolicyEngine.cs`. (depends on T020,
      T053)
- [ ] T055 [US2] Implement the fail-fast exception/message construction naming the
      enum, member, code, and referencing table (in-use case) or colliding field name
      (collision case) in `src/DataDictionary.Core/Sync/DataDictionarySyncException.cs`.
      (depends on T054)
- [ ] T056 [US2] Implement `ValidateOnly` mode in the startup synchronization
      orchestrator (compute the outcome, apply the breaking-change policy, but never
      call `ApplyAsync`) in `src/DataDictionary.Core/Sync/DataDictionarySynchronizer.cs`.
      (depends on T046, T054)

**Checkpoint**: User Stories 1 AND 2 both work independently — T048–T051 pass.

---

## Phase 5: User Story 3 - Incremental sync only touches what changed (Priority: P2)

**Goal**: Adding one member to an already-synced enum inserts only that member; every
other row, including its `updated_at`, stays untouched.

**Independent Test**: Run `quickstart.md` Scenario B; confirm exactly one insert and no
other row's `updated_at` changes.

### Tests for User Story 3 ⚠️

- [ ] T057 [P] [US3] Add a Core unit test asserting an unchanged entry appears in
      neither `ToInsert` nor `ToUpdate` (verified via `content_hash` comparison) in
      `tests/DataDictionary.Core.Tests/DiffEngineNoOpTests.cs`.
- [ ] T058 [P] [US3] Add an EF Core integration test for `quickstart.md` Scenario B (one
      member added, restart, exactly one insert, every other row's `updated_at`
      unchanged) in
      `tests/DataDictionary.EntityFrameworkCore.Tests/IdempotentSyncTests.cs`.

### Implementation for User Story 3

- [ ] T059 [US3] Implement `content_hash` computation over the fields compared for
      change detection — description, group, sort order, deprecated flag, per
      `data-model.md`'s `content_hash` note and `spec.md` Assumptions — in
      `src/DataDictionary.Core/Sync/DictionaryEntryHasher.cs`.
- [ ] T060 [US3] Extend the diff engine's update classification (`ToUpdate` populated
      only when `content_hash` differs; unchanged entries recorded in `Unchanged` and
      never written) in `src/DataDictionary.Core/Sync/DictionaryDiffEngine.cs`. (depends
      on T059)
- [ ] T061 [US3] Implement `IDataDictionaryStore.ApplyAsync` (EF Core) update path,
      writing `updated_at` only for rows actually present in `ToUpdate` — never for
      `Unchanged` rows — in
      `src/DataDictionary.EntityFrameworkCore/EfDataDictionaryStore.cs`. (depends on
      T045, T060)

**Checkpoint**: US1–US3 all independently functional — T057/T058 pass.

---

## Phase 6: User Story 4 - Removed-but-unused codes are retired (Priority: P2)

**Goal**: A member removed from an enum whose code is not referenced anywhere is marked
`is_active = 0`, not deleted, and boot proceeds normally.

**Independent Test**: Run `quickstart.md` Scenario D; confirm the row is deactivated and
boot succeeds.

### Tests for User Story 4 ⚠️

- [ ] T062 [P] [US4] Add a Core unit test classifying a removed-and-unused member into
      `ToDeactivate` in `tests/DataDictionary.Core.Tests/DiffEngineDeactivateTests.cs`.
- [ ] T063 [P] [US4] Add an EF Core integration test for `quickstart.md` Scenario D
      (removed unused code → `is_active` becomes `false`, boot succeeds) in
      `tests/DataDictionary.EntityFrameworkCore.Tests/RetirementTests.cs`.

### Implementation for User Story 4

- [ ] T064 [US4] Extend the diff engine's classification to route removed-and-not-in-use
      members into `ToDeactivate` (using T052's `IsCodeInUseAsync` result) in
      `src/DataDictionary.Core/Sync/DictionaryDiffEngine.cs`. (depends on T053)
- [ ] T065 [US4] Implement `IDataDictionaryStore.ApplyAsync` (EF Core) deactivation path
      — `is_active = false`, row never deleted — in
      `src/DataDictionary.EntityFrameworkCore/EfDataDictionaryStore.cs`. (depends on
      T061, T064)

**Checkpoint**: US1–US4 all independently functional — T062/T063 pass.

---

## Phase 7: User Story 5 - Multiple replicas can start at once safely (Priority: P2)

**Goal**: Two instances starting synchronization at once never conflict — exactly one
performs the write, and a replica that cannot acquire the lock falls back to
`ValidateOnly` behavior instead of failing or writing unsynchronized.

**Independent Test**: Run `quickstart.md` Scenario F (two simultaneous instances) and
confirm no key-conflict failure and exactly one writer.

### Tests for User Story 5 ⚠️

- [ ] T066 [P] [US5] Add an EF Core integration test (SQL Server and PostgreSQL) for
      `quickstart.md` Scenario F — two instances starting simultaneously in `Sync` mode,
      exactly one writes, neither fails on a key conflict — in
      `tests/DataDictionary.EntityFrameworkCore.Tests/ConcurrentBootTests.cs`.
- [ ] T067 [P] [US5] Add an EF Core integration test asserting an instance that fails to
      acquire the lock falls back to `ValidateOnly` behavior (no write, no startup
      failure caused solely by the lock) in
      `tests/DataDictionary.EntityFrameworkCore.Tests/LockFallbackTests.cs`.

### Implementation for User Story 5

- [ ] T068 [US5] Implement `IDataDictionaryStore.AcquireLockAsync` for SQL Server using
      `sp_getapplock` in
      `src/DataDictionary.EntityFrameworkCore/SqlServer/SqlServerDictionaryLock.cs`, per
      `research.md` §6. (depends on T044)
- [ ] T069 [P] [US5] Implement `IDataDictionaryStore.AcquireLockAsync` for PostgreSQL
      using `pg_advisory_lock` (session-scoped, released on session end) in
      `src/DataDictionary.EntityFrameworkCore/PostgreSql/PostgreSqlDictionaryLock.cs`,
      per `research.md` §6. (depends on T044)
- [ ] T070 [US5] Wire the startup synchronization orchestrator to call
      `AcquireLockAsync` before any write in `Sync`/`SyncAndValidate` modes, falling back
      to `ValidateOnly` behavior when the lock is not obtained (FR-024), in
      `src/DataDictionary.Core/Sync/DataDictionarySynchronizer.cs`. (depends on T056,
      T068, T069)

**Checkpoint**: All five user stories independently functional — T066/T067 pass.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Everything explicitly out of the per-story MVP but required before this
feature is considered implementation-complete: the opt-in seed strategy, packaging,
CI/CD, documentation, and the final constitution-compliance gate.

- [ ] T071 [P] Implement `SeedStrategy.Migration` via EF Core `HasData`, relying on the
      natural composite key `(enum_key, field_name)` already in place, in
      `src/DataDictionary.EntityFrameworkCore/Seeding/MigrationSeedStrategy.cs`, per
      `research.md` §8.
- [ ] T072 [P] Add an integration test proving `HasData`-seeded rows satisfy the same
      diff engine expectations as runtime-seeded ones, in
      `tests/DataDictionary.EntityFrameworkCore.Tests/MigrationSeedStrategyTests.cs`.
      (depends on T071)
- [ ] T073 [P] Reserve the `DD0008` diagnostic ID as an unused constant in
      `src/DataDictionary.Generator/Diagnostics/DiagnosticDescriptors.cs`. Per
      `contracts/diagnostics-contract.md`, this ID MUST NOT be wired to fire in this
      feature (no baseline lock file ships in this MVP).
- [ ] T074 Configure NuGet packaging for `DataDictionary.Generator` — `analyzers/dotnet/cs`
      output path, `DevelopmentDependency=true`, `PrivateAssets="all"` on every
      `Microsoft.CodeAnalysis.*` reference — in
      `src/DataDictionary.Generator/DataDictionary.Generator.csproj`, per `research.md`
      §5.
- [ ] T075 [P] Configure `DataDictionary.Abstractions` as an ordinary (non-private)
      transitive dependency of the main consumer-facing package, per `research.md` §5.
- [ ] T076 [P] Write `.github/workflows/ci.yml` building the full solution and running
      all three test projects, including the Testcontainers-backed integration tests, on
      push/PR.
- [ ] T077 [P] Write `.github/workflows/release.yml` packing and publishing every NuGet
      package on a version tag.
- [ ] T078 Write `README.md` (repository root) with Portuguese and English sections,
      including the copyable `RacaCor` example producing the row `RacaCor | Branca | B |
      1 | Branca` — Constitution Principle IX and `quickstart.md` Scenario A.
- [ ] T079 Audit every public type/member introduced in T012–T070 for XML documentation
      comments across `src/DataDictionary.Abstractions`, `src/DataDictionary.Generator`,
      `src/DataDictionary.Core`, and `src/DataDictionary.EntityFrameworkCore`, confirming
      the solution builds clean under `TreatWarningsAsErrors=true` (Constitution
      Principle IX).
- [ ] T080 Run every scenario in `quickstart.md` (A–G) end-to-end against
      `samples/Sample.Api` and the full automated test suite, confirming all pass, as the
      final constitution-compliance gate for this feature.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS every user story.
- **User Stories (Phase 3–7)**: All depend on Foundational completion.
  - US1 and US2 are both P1; US1 is the MVP and has no dependency on US2, but US2's
    `IsCodeInUseAsync` (T052) is reused by US4 (T064) — implement US2 before US4.
  - US3 depends only on Foundational + US1's `ApplyAsync` insert path (T045) to extend.
  - US4 depends on US2's `IsCodeInUseAsync` (T052) and US3's `ApplyAsync` extension
    point (T061).
  - US5 depends only on Foundational's `GetCurrentAsync` (T044); it does not require
    US2–US4 to be done first, but the orchestrator wiring in T070 extends whichever
    orchestrator state T056 (US2) left behind — so implement US2 before US5's T070.
- **Polish (Phase 8)**: Depends on all five user stories being complete.

### Recommended completion order

Setup → Foundational → US1 (P1, MVP) → US2 (P1) → US3 (P2) → US4 (P2) → US5 (P2) →
Polish. This matches spec.md's priority order and plan.md's Implementation Slices.

### Within Each User Story

- Tests MUST be written and FAIL before the implementation tasks in that story.
- Diff engine changes before store (`IDataDictionaryStore`) implementation changes.
- Store implementation before orchestrator wiring.
- Story complete (checkpoint reached) before moving to the next priority.

### Parallel Opportunities

- All Setup tasks marked [P] (T002–T009, T011) can run in parallel.
- Within Foundational, T012–T021 (Abstractions types) can all run in parallel; T025/T026
  (generator snapshot tests) and T028–T032 (diagnostics DD0002/DD0003/DD0004/DD0005/
  DD0006) can run in parallel once their stated dependencies are met.
- All [P]-marked tests within a single user story phase can run in parallel.
- US3, US4, and US5's test tasks (T057/T058, T062/T063, T066/T067) can be authored in
  parallel by different people once Foundational and US1/US2 implementation land, since
  each story's tests target a different scenario file.

---

## Parallel Example: Foundational Abstractions

```bash
# Launch all Abstractions contract-type tasks together:
Task: "Define DataDictionaryAttribute and DictionaryValueAttribute in src/DataDictionary.Abstractions/Attributes/"
Task: "Define DataDictionaryDefaultsAttribute and DataDictionaryScanAttribute in src/DataDictionary.Abstractions/Attributes/"
Task: "Define CodeSource and DescriptionSource enums in src/DataDictionary.Abstractions/"
Task: "Define EnumDictionaryModel and DictionaryMemberModel in src/DataDictionary.Abstractions/Manifest/"
Task: "Define DataDictionaryManifest and ManifestEnumEntry in src/DataDictionary.Abstractions/Manifest/"
Task: "Define DictionaryEntry and DictionaryEnumCatalogEntry in src/DataDictionary.Abstractions/Persistence/"
```

## Parallel Example: User Story 1 tests

```bash
Task: "Core unit test for insert-only diff classification in tests/DataDictionary.Core.Tests/DiffEngineInsertTests.cs"
Task: "EF Core integration test for quickstart.md Scenario A in tests/DataDictionary.EntityFrameworkCore.Tests/FreshSyncTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (blocks everything else).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: run `quickstart.md` Scenario A end-to-end; T041/T042 green.
5. This is the smallest state in which the library delivers its core value proposition
   (spec.md User Story 1's "Why this priority").

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → validate independently → MVP.
3. US2 → validate independently → production-safe (fail-fast) MVP.
4. US3 → validate independently → safe for routine repeated use.
5. US4 → validate independently → unused-code retirement without manual DB work.
6. US5 → validate independently → safe for multi-replica deployment.
7. Polish → packaging, docs, CI/CD, final constitution-compliance gate (T080).

### Parallel Team Strategy

With multiple developers, after Foundational is done: one developer can take US1 (the
critical path to MVP) while another starts US5's lock implementations (T068/T069), since
US5 depends only on Foundational's `GetCurrentAsync`, not on US1's insert path. US3 and
US4 are best sequenced after US1/US2 land, since they extend the same
`DictionaryDiffEngine`/`EfDataDictionaryStore` files US1/US2 create.

---

## Notes

- [P] tasks touch different files with no unmet dependency.
- [Story] labels (US1–US5) trace every task back to `spec.md`'s prioritized user stories.
- Every field-level constraint quoted in a task above (column lists, enum default
  values, diagnostic severities) is taken verbatim from `data-model.md`,
  `spec.md`, or `contracts/` — implementers should not need to re-derive them.
- No task in this file creates a `.cs`/`.csproj`/`.sln` file as a side effect of
  `/speckit-tasks` itself; this document only records what those future tasks are.
- `/speckit-implement` is explicitly out of scope until a human approves proceeding past
  this planning phase.
