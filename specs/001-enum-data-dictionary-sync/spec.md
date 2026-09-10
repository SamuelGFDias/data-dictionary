# Feature Specification: Enum-Sourced Data Dictionary Sync

**Feature Branch**: `001-enum-data-dictionary-sync`

**Created**: 2026-09-10

**Status**: Draft

**Input**: User description: "Times de dados e BI não sabem quais são os valores válidos das colunas que guardam enums da aplicação (a coluna guarda 'B', ninguém sabe que é 'Branca' do enum RacaCor). A documentação vive em planilha e apodrece. Queremos que o banco carregue um dicionário de dados sempre sincronizado com o código. Uma lib .NET onde o enum C# é a fonte da verdade. Um source generator lê os enums marcados, gera um manifesto em compile-time, o mapeamento EF da tabela de dicionário e os seeders. Na inicialização a aplicação sincroniza o dicionário e falha rápido diante de mudança destrutiva."

## Clarifications

### Session 2026-09-10

- Q: What is the out-of-the-box default for the breaking-change policy (FR-020) when a
  developer does not explicitly configure it? → A: **Fail** — an unconfigured
  application fails startup on an in-use-code-removed or code-collision divergence,
  consistent with the project's fail-fast, no-silent-write posture. *(This is a default
  value on a public configuration option, not new functionality; flagged for human
  confirmation — see the feature's implementation notes.)*
- Q: What is the out-of-the-box default synchronization mode (FR-013) when a developer
  does not explicitly configure it? → A: **Off** — the library performs no check and no
  write until a developer explicitly opts an environment into `ValidateOnly`, `Sync`, or
  `SyncAndValidate`; adopting the library is inert by default. *(Default value on a
  public configuration option; flagged for human confirmation.)*
- Q: What is the out-of-the-box default column-naming convention and default table name
  (FR-011) when a developer does not explicitly configure them? → A: **snake_case**
  columns, matching every column name already used throughout this specification (e.g.
  `enum_key`, `field_name`, `code`, `numeric_value`, `is_active`), with default table
  names `tb_dicionario_dados` and `tb_dicionario_enum` in the database's default schema.
  *(Default value on a public configuration option; flagged for human confirmation.)*

## User Scenarios & Testing *(mandatory)*

<!--
  Note on personas: DataDictionary is a developer-facing library. Its "users" are the
  backend engineers who mark enums and configure sync, and — one hop downstream — the
  data/BI analysts who read the resulting dictionary table. Both personas appear below.
-->

### User Story 1 - First sync populates the dictionary from the enum (Priority: P1)

A backend developer marks an existing C# enum (e.g. `RacaCor`) as a data dictionary
source and its members with their string codes. The team starts the application against
an empty database in synchronization mode. The dictionary table is populated
automatically, one row per enum member, with no hand-written SQL and no spreadsheet.

**Why this priority**: This is the entire value proposition of the library. Without a
working first sync, nothing else in the feature matters — it is the smallest possible
slice that already eliminates the rotting-spreadsheet problem for one enum.

**Independent Test**: Mark one enum, point a fresh/empty database at the application,
start it in sync mode, and query the dictionary table directly — it can be fully
verified without any of the later stories (idempotency, fail-fast, locking) existing yet.

**Acceptance Scenarios**:

1. **Given** an enum marked as a data dictionary source and an empty database, **When**
   the application starts in Sync mode, **Then** the dictionary table contains exactly
   one row per enum member, each with its correct code, numeric value, and description.
2. **Given** an enum with two members that resolve to the same code, **When** the
   solution is built, **Then** the build fails with a dedicated, stable diagnostic
   identifying the duplicate.

---

### User Story 2 - Destructive divergence aborts the boot (Priority: P1)

A member is removed from an enum, but rows keyed by its code are still referenced by a
business table. When the application starts again, it must not silently orphan or
mislabel that data — it must refuse to boot and explain exactly what is wrong and to
whom.

**Why this priority**: This is the safety guarantee that makes automatic sync trustworthy
in production. Without it, the library would be indistinguishable from "an automated way
to silently corrupt or strand data," which is worse than the manual spreadsheet it
replaces. It must exist before any team is allowed to run Sync mode against a real
database.

**Independent Test**: Seed a database with a dictionary entry whose code is referenced by
a row in a business table, remove the corresponding member from the enum, start the
application, and confirm the boot fails with an actionable message — independent of
whether idempotent updates or distributed locking are implemented yet.

**Acceptance Scenarios**:

1. **Given** a member removed from an enum whose code is still referenced by a business
   table, **When** the application starts, **Then** the boot fails with a message naming
   the enum, the member, the code, and the business table that references it.
2. **Given** a member's code changed to a value that collides with another existing entry
   for the same field, **When** the application starts, **Then** the boot fails rather
   than overwriting the pre-existing entry.
3. **Given** the synchronization mode is `ValidateOnly` and any divergence exists between
   the enum and the stored dictionary, **When** the application starts, **Then** the boot
   fails and nothing is written to the database.

---

### User Story 3 - Incremental sync only touches what changed (Priority: P2)

Months after the first sync, a developer adds one new member to an already-dictionaried
enum. On the next deploy, only that new entry is written; every other row — including
its `updated_at` timestamp — is left untouched.

**Why this priority**: Confirms the library is safe for routine, repeated use over the
life of a project, not just a one-time import tool. It depends on Story 1 existing (there
must already be a dictionary to update) but is independently verifiable without the
fail-fast or locking behavior.

**Independent Test**: Run Story 1's scenario, add one enum member, restart the
application, and verify only the new row is inserted and no other row's `updated_at`
changed.

**Acceptance Scenarios**:

1. **Given** a dictionary already synchronized once, **When** one member is added to the
   enum and the application starts again, **Then** only the new entry is inserted and no
   other row is modified.
2. **Given** a member's description, group, sort order, or deprecated flag changed in the
   enum's source code, **When** the application starts, **Then** only that entry's
   corresponding columns are updated.
3. **Given** no change at all between the enum and the stored dictionary, **When** the
   application starts, **Then** no row is written and no row's `updated_at` changes.

---

### User Story 4 - Removed-but-unused codes are retired, not deleted (Priority: P2)

A member is removed from an enum, and its code was never actually used in any business
table. On the next start, the corresponding dictionary entry is marked inactive rather
than deleted, preserving history for anyone who needs to interpret old records or
reports, while the boot proceeds normally.

**Why this priority**: This is what makes the fail-fast behavior of Story 2 tolerable in
practice — teams need a path to retire genuinely unused codes without a manual database
intervention every time. It builds directly on Story 2's in-use/not-in-use distinction.

**Independent Test**: Seed a dictionary entry whose code has zero references anywhere in
business data, remove the member from the enum, start the application, and confirm the
row is marked inactive and the boot completes successfully.

**Acceptance Scenarios**:

1. **Given** a member removed from an enum whose code is not referenced anywhere in
   business data, **When** the application starts, **Then** the corresponding entry is
   marked inactive and the boot completes without error.

---

### User Story 5 - Multiple replicas can start at once safely (Priority: P2)

An application with several running instances is deployed at once. All instances start
in Sync mode simultaneously. Exactly one of them performs the synchronization; the others
detect that sync is already underway and proceed without attempting a conflicting write
or failing the deployment.

**Why this priority**: Any real deployment target (containers, multiple replicas, rolling
restarts) violates the single-writer assumption implicit in Stories 1–4 unless this is
handled. It is scoped as its own story because it is orthogonal to the diff/fail-fast
logic and independently testable with a trivial dictionary (or even Story 1's scenario
run twice in parallel).

**Independent Test**: Start two instances of the same application against the same empty
database at the same time, both in Sync mode, and confirm the dictionary ends up
correctly populated with no write conflict and no failed startup.

**Acceptance Scenarios**:

1. **Given** two application instances starting at the same time in Sync mode against the
   same database, **When** both attempt synchronization, **Then** exactly one performs
   the write, the other proceeds without error, and neither fails due to a key conflict.

---

### Edge Cases

- What happens when an enum member's code cannot be resolved by any configured source
  (no XML doc, no explicit code, convention mode disabled)? → Build fails with a
  dedicated diagnostic; nothing about this is decided at runtime.
- What happens when two different enums are marked with the same dictionary key? → Build
  fails with a dedicated diagnostic; the application never starts with an ambiguous
  manifest.
- What happens when a marked enum is also a `[Flags]` enum? → Rejected at build time; a
  `[Flags]` enum can never be marked as a dictionary source in this version of the
  library.
- What happens when two members of the same enum share the same underlying numeric
  value (an alias)? → Allowed, but flagged at build time as a warning; both are persisted
  as separate dictionary entries since they have distinct codes.
- What happens when the distributed lock cannot be acquired by any instance within a
  reasonable time (e.g. the lock holder crashed mid-sync)? → Covered by the
  synchronization mode fallback behavior (a replica that cannot obtain the lock proceeds
  in validate-only behavior rather than hanging indefinitely or writing unsynchronized).
- What happens in `Off` mode? → The library performs no check and no write at startup at
  all; the dictionary is left exactly as it was.
- What happens when `RequireDescription` is set and a member truly has no description
  from any source? → Flagged at build time as a warning (not a hard failure), and the
  configured fallback (member name) is used at runtime.

## Requirements *(mandatory)*

<!--
  DataDictionary is a library; its "product" is a public contract (attributes, generated
  members, configuration options, and persisted schema). That contract is listed
  explicitly below because, for this feature, the contract itself is the requirement —
  the same way a CLI tool's flags are the requirement for a CLI spec.
-->

### Functional Requirements

**Marking enums as dictionary sources**

- **FR-001**: The library MUST let a developer mark an enum as a data dictionary source,
  supplying a dictionary key and an optional group name for that enum.
- **FR-002**: The library MUST let a developer mark an individual enum member with an
  explicit string code and an explicit deprecated flag.
- **FR-003**: The library MUST support a convention mode, declared once at the assembly
  level, that derives codes and descriptions for every enum member without requiring a
  per-member attribute — configuring where the code comes from (e.g. the member's own
  name), where the description comes from, and whether a description is required.
- **FR-004**: The library MUST support declaring, at the assembly level, which
  namespace(s) are scanned for dictionary-eligible enums under convention mode.
- **FR-005**: The library MUST resolve each entry's description using a configurable,
  ordered precedence: the member's XML documentation summary, then an explicit
  description attribute, then an explicit display-name attribute, then the member's own
  name as the final fallback.
- **FR-006**: The library MUST reject, at build time, any enum decorated with the
  `[Flags]` attribute that is also marked as a dictionary source.

**Persisted shape**

- **FR-007**: The library MUST persist, for every dictionary entry, both the enum's
  string code and its underlying numeric value in the same row.
- **FR-008**: The library MUST persist all dictionary entries, across every marked enum,
  in a single generic table keyed naturally by the combination of the enum's dictionary
  key and the entry's field name, storing: code, numeric value, description, group name,
  active flag, deprecated flag, sort order, a content hash, a creation timestamp, and a
  last-updated timestamp.
- **FR-009**: The library MUST enforce, per enum key, that only one entry per code may be
  active at a time (a uniqueness guarantee scoped to active entries).
- **FR-010**: The library MUST support an optional catalog table, one row per marked
  enum, recording the enum's dictionary key, its full CLR type name, its declaring
  assembly, its description, its group name, a manifest hash, and the last synchronization
  timestamp.
- **FR-011**: The library MUST let a developer configure the table name(s), the schema,
  and the column-naming convention (e.g. PascalCase vs. snake_case) used for the
  generated persisted shape. Absent explicit configuration, the default column-naming
  convention MUST be snake_case and the default table names MUST be
  `tb_dicionario_dados` and `tb_dicionario_enum`, in the database's default schema.
- **FR-012**: The library MUST provide a single generated entry point that configures the
  dictionary's own persisted shape and wires the generated conversion between every
  business entity property whose type is a marked enum and its persisted code, in one
  call.

**Startup synchronization**

- **FR-013**: The library MUST support, as a startup configuration choice, at least four
  synchronization modes: doing nothing, validating without writing, writing, and
  validating-then-writing. Absent explicit configuration, the default mode MUST be
  "doing nothing" (`Off`), so adopting the library never writes to or even queries the
  database until a developer explicitly opts an environment in.
- **FR-014**: The library MUST compute, at startup, the difference between the compiled
  enum manifest and the currently stored dictionary, and MUST classify every difference
  into exactly one of: a new entry to insert, an existing entry whose descriptive fields
  changed and must be updated, an entry whose code is no longer used anywhere and may be
  deactivated, an entry whose code is still in use and must not be silently touched, or a
  code collision that must not be silently resolved.
- **FR-015**: The library MUST insert a dictionary row for every enum member present in
  the manifest but absent from the stored dictionary.
- **FR-016**: The library MUST update a dictionary row's description, group, sort order,
  or deprecated flag when the manifest's value for that field differs from the stored
  value, and MUST leave rows whose fields are unchanged untouched (including their
  last-updated timestamp).
- **FR-017**: The library MUST mark a dictionary row inactive, rather than deleting it,
  when its corresponding enum member has been removed and no business data references
  its code.
- **FR-018**: The library MUST abort application startup, performing no writes, when an
  enum member has been removed but its code is still referenced by business data,
  reporting the enum, the member, the code, and the referencing table in the failure.
- **FR-019**: The library MUST abort application startup, performing no writes, when a
  member's code has changed to a value that collides with an existing entry for the same
  field, rather than overwriting the pre-existing entry.
- **FR-020**: The library MUST let a developer configure how the two abort-worthy
  divergences above (in-use code removed; code collision) are handled — failing startup,
  warning without failing, or ignoring — as a single policy setting. Absent explicit
  configuration, the default policy MUST be "fail startup".
- **FR-021**: When synchronization mode is validate-only, the library MUST detect the
  same divergences as write-capable modes but MUST NOT write anything to the database,
  and MUST fail startup if any divergence that the configured policy treats as fatal is
  found.
- **FR-022**: The library MUST determine whether a given code is "in use" by inspecting
  which business data actually references it, not merely by checking the dictionary
  table's own bookkeeping columns.

**Concurrent startup safety**

- **FR-023**: The library MUST coordinate synchronization across multiple application
  instances starting at the same time so that exactly one instance performs the
  synchronization write and no instance fails due to a write conflict.
- **FR-024**: An instance that cannot obtain the synchronization coordination lock MUST
  fall back to validating without writing, rather than failing startup or writing
  unsynchronized.

**Build-time diagnostics**

- **FR-025**: The library MUST report a build-time diagnostic when an enum member's code
  cannot be resolved by any configured source.
- **FR-026**: The library MUST report a build-time diagnostic when two members of the
  same enum resolve to the same code.
- **FR-027**: The library MUST report a build-time diagnostic when a resolved code
  exceeds a configurable maximum length.
- **FR-028**: The library MUST report a build-time diagnostic (non-fatal) when a member
  has no resolvable description and the configuration requires one.
- **FR-029**: The library MUST report a build-time diagnostic when two different enums
  are marked with the same dictionary key.
- **FR-030**: The library MUST report a build-time diagnostic (non-fatal) when two
  members of the same enum share the same underlying numeric value.
- **FR-031**: The library MUST report a build-time diagnostic when a `[Flags]` enum is
  marked as a dictionary source.
- **FR-032**: Every build-time diagnostic the library reports MUST surface as a build
  error or warning in the consuming project's own build output, never only as a runtime
  failure.

### Key Entities

- **Dictionary Entry**: One row of the generic dictionary table. Represents a single
  enum member's persisted identity: which enum it belongs to, its field name, its string
  code, its numeric value, its human description, its group, whether it is active,
  whether it is deprecated, its display order, a content hash used to detect changes, and
  when it was created/last updated.
- **Enum Catalog Entry**: One row of the optional catalog table. Represents a marked enum
  itself (not its members): its dictionary key, its CLR type identity, its declaring
  assembly, its description, its group, a hash of its whole manifest, and when it was
  last synchronized.
- **Compiled Manifest**: The complete, compile-time-produced description of every marked
  enum and member in the compiled assembly — the source of truth that startup
  synchronization compares against what is stored.
- **Synchronization Outcome**: The result of comparing the compiled manifest against the
  stored dictionary at startup — which entries to insert, update, deactivate, or which
  divergences require aborting startup.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a newly marked enum synchronized against an empty database, 100% of the
  enum's members are represented as dictionary rows with correct code, numeric value, and
  description, with no manual database steps.
- **SC-002**: Adding one member to an already-synchronized enum and restarting the
  application changes exactly one dictionary row (the new insert); zero pre-existing rows
  have their last-updated timestamp changed.
- **SC-003**: 100% of startup attempts that would silently strand or mislabel
  in-use business data are instead blocked before any write occurs, with a failure
  message that identifies the enum, the member, the code, and the affected table.
- **SC-004**: 100% of startup attempts made while validate-only mode is active leave the
  database completely unwritten, whether or not a divergence is found.
- **SC-005**: When multiple application instances start synchronization at the same
  moment, 100% of such startups complete without a write-conflict failure, and exactly
  one instance performs the write.
- **SC-006**: 100% of the enum-authoring mistakes covered by this feature's build-time
  diagnostics (unresolvable code, duplicate code, oversized code, duplicate dictionary
  key, `[Flags]` misuse) are caught while building the consuming project, with zero of
  them reaching a running application undetected.
- **SC-007**: A data or BI analyst can look up the meaning of any code stored in a
  dictionaried column by querying the database alone, without consulting an external
  spreadsheet or the application's source code.

## Assumptions

- The consuming application uses a relational database reachable at startup; the
  dictionary table(s) described here live in that same database.
- "In use" for a code is determined by scanning the consuming application's own data
  model for properties typed as the marked enum, not by an external declaration of which
  tables matter.
- A reasonable default exists, and is used unless the developer overrides it, for exactly
  which fields are compared to decide whether a dictionary entry has "changed" (FR-016):
  description, group, sort order, and deprecated flag; the code and numeric value are
  treated as immutable identity for a given field name (a change to either is a
  collision/removal case, not an update).
- Only one alternate persistence technology and one alternate seeding technology are in
  scope for the MVP described here (see Out of Scope); the library's internal contracts
  are expected to accommodate more without a rewrite, but no second provider ships in
  this feature.
- `[Flags]` enums, a Dapper provider, SQL/CSV export, a CLI, and an automatically-
  generated migration are explicitly out of scope for this feature (see Out of Scope) —
  their absence is a deliberate scope boundary, not an oversight.

## Out of Scope (MVP)

- A Dapper-based provider (only the object-relational-mapper-based provider family
  described in FR-012 ships in this feature).
- Exporting the dictionary to SQL or CSV.
- A command-line interface for inspecting or managing the dictionary.
- A versioned baseline file used to detect a code changed on an existing member across
  builds (i.e., the collision case in FR-019 is detected against the live database at
  startup, not against a build-time baseline file, in this feature).
- Support for `[Flags]` enums as dictionary sources.
- Automatic generation of database migration scripts for the dictionary table's own
  schema.
