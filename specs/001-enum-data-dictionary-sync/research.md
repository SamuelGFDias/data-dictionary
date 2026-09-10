# Phase 0 Research: Enum-Sourced Data Dictionary Sync

All items below were "NEEDS CLARIFICATION" candidates in the Technical Context section
of `plan.md`, or open technical unknowns implied by the feature spec and the project
constitution. Each is resolved with a decision, rationale, and alternatives considered.

## 1. Why split Abstractions/Generator (netstandard2.0) from Core/providers (net10.0)

- **Decision**: `DataDictionary.Abstractions` and `DataDictionary.Generator` target
  `netstandard2.0` exclusively. `DataDictionary.Core` and every provider target `net10.0`
  exclusively. No project multi-targets across this boundary.
- **Rationale**: A Roslyn source generator loads into the *compiler's own process*, which
  is frequently pinned to an older runtime than the project being compiled (older IDE
  versions, older SDKs invoked via `dotnet build`, non-.NET-10 CI images building a
  net10.0 app that merely *references* this generator). `netstandard2.0` is the
  documented floor for maximum Roslyn host compatibility. `Abstractions` must be
  `netstandard2.0` too because the generator inspects/authors code against its attribute
  types at compile time, and because `Abstractions` is also referenced by `Core` — so it
  needs to be loadable everywhere `Core` is (net10.0) *and* everywhere the generator runs
  (host process). `Core` itself has no such constraint and gains real benefit (AOT,
  trimming, modern BCL) from being net10.0-only.
- **Alternatives considered**: Multi-targeting `Abstractions` for both `netstandard2.0`
  and `net10.0` — rejected as unnecessary complexity; a single `netstandard2.0` build is
  binary-compatible with net10.0 consumers and avoids maintaining two API surfaces for
  the same types. Targeting the generator at `netstandard2.1` — rejected, not universally
  supported by every Roslyn host `netstandard2.0` supports.

## 2. Provider scope for the MVP

- **Decision**: The only shipped provider is `DataDictionary.EntityFrameworkCore`,
  supporting SQL Server and PostgreSQL as backing databases (both explicitly named by the
  distributed-lock requirement in the spec). Dapper and any other data-access technology
  are out of scope per the spec's own "Out of Scope (MVP)" section.
- **Rationale**: `ApplyDataDictionary()` as a `ModelBuilder` extension method is an EF
  Core concept named directly in the spec; there is no ambiguity about which ORM ships
  first. SQL Server and PostgreSQL are the two databases the spec's lock requirement names
  explicitly, so both are treated as first-class within the one EF Core provider (not as
  two separate provider packages).
- **Alternatives considered**: Shipping a database-agnostic lock abstraction with only one
  concrete implementation — rejected; the spec asks for both, and Core's
  `IDataDictionaryStore.AcquireLockAsync` contract (see `data-model.md` /
  `contracts/store-contract.md`) already accommodates more providers later without
  reshaping the interface.

## 3. Generator incrementality strategy

- **Decision**: The generator is built on `IIncrementalGenerator` using
  `ForAttributeWithMetadataName` as the primary syntax/semantic entry point (one
  registration per marked-attribute shape: `DataDictionaryAttribute`,
  `DataDictionaryDefaultsAttribute`, `DataDictionaryScanAttribute`). Every model object
  that flows through the incremental pipeline (enum model, member model, manifest model)
  is an immutable, structurally-equatable record; any collection field uses
  `ImmutableArray<T>` paired with a custom `IEqualityComparer` (or a hand-rolled
  `Equals`/`GetHashCode` pair) rather than relying on reference equality or default
  `ImmutableArray` equality (which is reference-based and would defeat caching).
- **Rationale**: Roslyn's incremental generator cache keys pipeline stages on the
  equality of their output values. A model that is only reference-equal (the default for
  a plain class, and effectively also for `ImmutableArray<T>.Equals`, which compares the
  underlying array reference) invalidates on every keystroke even when nothing relevant
  changed, defeating the entire point of `IIncrementalGenerator` and causing IDEs to
  re-run the generator on every edit anywhere in the file. Structural equality on
  immutable models is the documented pattern for keeping the cache warm.
- **Alternatives considered**: The legacy `ISourceGenerator` with a syntax receiver —
  rejected, non-incremental, already deprecated in favor of `IIncrementalGenerator`, and
  the constitution's "AOT and trimming compatible" / "zero hot-path reflection"
  principles have no bearing on generator internals, so there is no offsetting reason to
  accept the worse IDE experience.

## 4. How the compiled manifest reaches Core at runtime

- **Decision**: For every compilation containing at least one marked enum, the generator
  emits one partial, internal bootstrapper type whose static entry point calls
  `services.AddDataDictionary(builder => builder.AddManifest(DataDictionaryManifest.Default))`
  (exact method names are part of the public contract and are recorded in
  `contracts/generated-entrypoints-contract.md`). `DataDictionaryManifest.Default` is
  itself a generated, `static readonly` instance built from compile-time-known data only
  (arrays of structs/records, no reflection, no attribute inspection at runtime).
- **Rationale**: This is the direct mechanism satisfying the constitution's zero-hot-path-
  reflection and AOT/trimming principles: the manifest is data baked into the assembly at
  compile time, and wiring it into DI is one generated call the consumer's own `Program`
  invokes (or that a generated `IHostedService`/startup filter invokes automatically,
  exact wiring left to `Core`'s DI extension design in the implementation phase).
- **Alternatives considered**: Runtime assembly scanning for marked enums via reflection
  — rejected outright, this is precisely the hot/startup-path reflection the constitution
  prohibits at the generator/manifest boundary; even though it would only run once at
  startup and not in the "hot path" strictly defined as steady-state request handling,
  the spec explicitly says the generator — not runtime reflection — produces the
  manifest, and multi-assembly scanning would fight AOT trimming by requiring every
  candidate assembly to preserve enum metadata.

## 5. NuGet packaging shape for the generator

- **Decision**: The `DataDictionary.Generator` project's output DLL is packed into
  `analyzers/dotnet/cs/` inside the NuGet package (not `lib/`), the package sets
  `DevelopmentDependency=true` (`<PackageType>DevelopmentDependency</PackageType>` /
  `<developmentDependency>` metadata), and every `Microsoft.CodeAnalysis.*` package
  reference the generator project takes is marked `PrivateAssets="all"` so Roslyn's own
  assemblies never become a transitive dependency of a consuming project.
  `DataDictionary.Abstractions` is referenced as an ordinary (non-private) dependency of
  the main consumer-facing package so its attribute types are usable in consumer code and
  visible as a normal transitive package reference.
- **Rationale**: This is the standard, documented shape for a Roslyn analyzer/generator
  NuGet package; deviating from it either breaks the generator (wrong DLL location) or
  leaks build-only dependencies into consumer restore graphs.
- **Alternatives considered**: A single combined package containing both the generator and
  `Abstractions` — rejected; `Abstractions` types need to be an ordinary compile-time
  reference for consumer code (attributes applied directly in user source), which is
  incompatible with `DevelopmentDependency` packaging semantics for the whole package.

## 6. Distributed lock per provider

- **Decision**: The EF Core provider's `AcquireLockAsync` implementation uses each
  database's native advisory/application lock primitive: `sp_getapplock` on SQL Server,
  `pg_advisory_lock` (session-level, with a corresponding unlock/session-scoped release)
  on PostgreSQL. No external lock service (e.g. Redis, ZooKeeper) is introduced.
- **Rationale**: Both primitives are already available on the exact connection the
  synchronization process is using, require no new infrastructure dependency, and are
  released automatically on connection/session end, which bounds the worst case (a
  crashed instance) to "until that connection is torn down" rather than requiring a
  separate lock-expiry mechanism.
- **Alternatives considered**: A dedicated lock row in the dictionary catalog table with
  optimistic concurrency — rejected as a fallback-only design; it re-implements what the
  database already provides natively for both target databases and adds failure modes
  (stale locks from a crashed holder) that native advisory locks avoid by tying the lock
  to the connection/session lifetime.

## 7. `IsCodeInUseAsync` strategy

- **Decision**: `Core`'s diff engine calls `IsCodeInUseAsync` on the active
  `IDataDictionaryStore`. The EF Core provider implements it by walking the running
  application's `IModel` (`DbContext.Model`) for every entity property whose CLR type is
  the enum in question (using the generated manifest to know which CLR type corresponds
  to which `enum_key`), then querying, for the specific numeric/code value being
  retired, whether any row of that entity set currently holds it.
- **Rationale**: EF Core's model already knows the CLR type of every mapped property;
  deriving "which tables/columns use this enum" from that model means the developer never
  has to declare it by hand, matching the spec's requirement that this be automatic
  (FR-022) and the "why this priority" framing under User Story 2 — the entire value of
  the fail-fast guarantee depends on the library detecting usage itself.
- **Alternatives considered**: Requiring an explicit developer-authored list of
  "protected" tables/columns per enum — rejected; it is exactly the kind of documentation
  that goes stale, which is the root problem this feature exists to solve.

## 8. Seed strategy

- **Decision**: The default seeding strategy is a runtime seeder that executes as part of
  startup synchronization (the same diff engine described under FR-014–FR-019). A second,
  opt-in strategy (`SeedStrategy.Migration`) uses EF Core's `HasData` to bake dictionary
  rows into generated migrations instead. `SeedStrategy.Migration` is viable specifically
  because the dictionary table's primary key is the natural, deterministic composite
  `(enum_key, field_name)` — `HasData`'s snapshot-diffing model requires a stable key it
  can compare across migrations, which a natural key satisfies without introducing a
  surrogate key solely for this purpose.
- **Rationale**: Runtime seeding is required regardless (it is what makes the fail-fast
  diff/abort behavior possible at all — `HasData` alone cannot abort a boot). Exposing
  `HasData` as an *additional*, opt-in strategy costs little once the natural key exists,
  and some teams' deployment pipelines prefer migration-based seeding for auditability.
- **Alternatives considered**: Migration-only seeding as the default — rejected outright;
  it cannot express "abort boot if an in-use code was removed", which is a hard
  requirement (FR-018) that only a runtime check against live data can satisfy.

## 9. Testing stack

- **Decision**: `DataDictionary.Generator.Tests` uses `Verify` (`Verify.Xunit` or
  equivalent) together with `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` /
  `Microsoft.CodeAnalysis.Testing` for driving the generator against fixture sources and
  snapshotting emitted output and diagnostics. `DataDictionary.Core.Tests` uses xUnit for
  fast, provider-free unit tests of the diff engine. `DataDictionary.EntityFrameworkCore.Tests`
  uses xUnit plus Testcontainers (`Testcontainers.MsSql`, `Testcontainers.PostgreSql`) to
  run the real integration scenarios (fresh sync, idempotent sync, fail-fast, concurrent
  boot) against real, ephemeral database instances.
- **Rationale**: Directly mandated by the constitution ("Generator = snapshot test;
  provider = teste de integração com Testcontainers contra banco real") and by the task's
  explicit test-project naming.
- **Alternatives considered**: An in-memory EF Core provider for the "integration" tests
  — rejected; it cannot exercise `sp_getapplock`/`pg_advisory_lock` or real unique-index
  behavior, both of which are central to the feature's correctness guarantees (Stories 2
  and 5).

**Output of this phase**: no NEEDS CLARIFICATION markers remain in the Technical Context
of `plan.md`; all nine items above are resolved decisions carried forward into
`data-model.md` and `contracts/`.
