# Phase 1 Data Model: Enum-Sourced Data Dictionary Sync

Two layers of "model" exist in this feature and are kept distinct throughout: the
**compile-time model** the generator produces and reasons about, and the **runtime/
persisted model** `Core` and providers operate on. They are related (the compile-time
model is the source the runtime manifest is built from) but are not the same types.

## Compile-time models (generator, `DataDictionary.Generator`)

These live entirely inside the generator's incremental pipeline. Every one of them MUST
be an immutable, structurally-equatable type (record, or a class with hand-written
`Equals`/`GetHashCode`), and every collection field MUST be an `ImmutableArray<T>` paired
with explicit structural equality — see `research.md` §3.

### `EnumDictionaryModel`

Represents one marked enum, as seen by the compiler.

| Field | Type | Notes |
|---|---|---|
| `EnumKey` | `string` | From `[DataDictionary("...")]` or convention default (enum simple name). Must be unique across the compilation (DD0005 otherwise). |
| `GroupName` | `string?` | Optional. |
| `ClrFullName` | `string` | Fully-qualified CLR type name, used for the optional catalog table and for matching properties during `IsCodeInUseAsync`. |
| `AssemblyName` | `string` | Declaring assembly's simple name. |
| `Description` | `string?` | Resolved the same way member descriptions are (XML doc → attribute → name), applied to the enum itself for the catalog table. |
| `IsFlags` | `bool` | If `true`, this model is never emitted into the manifest — DD0007 fires instead. |
| `Members` | `ImmutableArray<DictionaryMemberModel>` | Structurally compared. |

**Validation rules** (enforced by the generator, surfaced as DDxxxx diagnostics — see
`contracts/diagnostics-contract.md`):
- `EnumKey` must be non-empty and unique across all `EnumDictionaryModel`s in the
  compilation (DD0005).
- `IsFlags` must be `false` (DD0007).
- `Members` must contain at least one entry with a resolvable `Code` (DD0001 otherwise,
  per member).

### `DictionaryMemberModel`

Represents one member of a marked enum.

| Field | Type | Notes |
|---|---|---|
| `FieldName` | `string` | The C# member name; this is the second half of the persisted natural key `(enum_key, field_name)`. |
| `Code` | `string` | Resolved per the precedence in FR-005 (source recorded in `CodeSource` for diagnostics). Must be unique within the enum (DD0002) and within `MaxCodeLength` (DD0003). |
| `NumericValue` | `long` | The member's underlying numeric value (widened to `long` to cover every enum backing type). |
| `Description` | `string?` | Resolved per FR-005. `null` only if `RequireDescription=false` and no source resolved (DD0004 fires when `RequireDescription=true` and this is `null`). |
| `GroupName` | `string?` | Falls back to the owning enum's `GroupName` if not set per-member. |
| `IsDeprecated` | `bool` | From `[DictionaryValue(Deprecated = ...)]`, default `false`. |
| `SortOrder` | `int` | Defaults to declaration order within the enum. |
| `CodeSource` | enum (`Explicit`, `XmlDoc`, `DescriptionAttribute`, `DisplayAttribute`, `MemberName`) | Diagnostic/debugging aid; not persisted. |

**Validation rules**:
- `Code` unique within the owning `EnumDictionaryModel.Members` (DD0002).
- `Code.Length <= MaxCodeLength` (DD0003, configurable, see Clarifications in `spec.md`
  for the still-open default value).
- `NumericValue` uniqueness across members of the same enum is **not** required; a
  collision is allowed but triggers DD0006 (Warning) because it signals a numeric alias.

### `DataDictionaryManifest` (generated, emitted into the compiled assembly)

The flattened, runtime-facing projection of every `EnumDictionaryModel` that passed
validation in a given compilation. `DataDictionaryManifest.Default` is the generated
`static readonly` instance consumers wire into DI (see `research.md` §4 and
`contracts/generated-entrypoints-contract.md`). Structurally, it is an
`ImmutableArray<ManifestEnumEntry>`, each `ManifestEnumEntry` holding the same fields as
`EnumDictionaryModel`/`DictionaryMemberModel` above, minus the generator-only
`CodeSource` diagnostic field.

## Runtime / persisted models (`Core`, `EntityFrameworkCore` provider)

### `DictionaryEntry` (maps to `tb_dicionario_dados`)

| Column | Type | Notes |
|---|---|---|
| `enum_key` | string | Part of natural composite PK. |
| `field_name` | string | Part of natural composite PK. |
| `code` | string | The resolved code. Unique per `enum_key` among rows where `is_active = 1` (filtered unique index). |
| `numeric_value` | integer (wide enough for `long`) | |
| `description` | string, nullable | |
| `group_name` | string, nullable | |
| `is_active` | boolean | `false` only via the "removed and unused" retirement path (FR-017); never hard-deleted. |
| `is_deprecated` | boolean | |
| `sort_order` | integer | |
| `content_hash` | string | Hash of the fields compared for "did this entry change" (description, group, sort order, deprecated flag — see `spec.md` Assumptions) — used by the diff engine to short-circuit unchanged rows without touching `updated_at`. |
| `created_at` | timestamp | Set once, on insert. |
| `updated_at` | timestamp | Set only when an update actually changes a compared field. |

**State transitions**: `(absent) → active` (insert, FR-015) · `active → active` (update
of descriptive fields only, FR-016; also the identity no-op when `content_hash` is
unchanged) · `active → inactive` (retirement, FR-017 — terminal for this feature; nothing
in this feature transitions a row back from `inactive` to `active`, since the removed
enum member no longer exists in the manifest to justify recreating it) · any transition
implying a `code`/`enum_key`/`field_name` identity change is **not a transition** — it is
either rejected (FR-019) or modeled as a new row entirely.

### `DictionaryEnumCatalogEntry` (maps to the optional `tb_dicionario_enum`)

| Column | Type | Notes |
|---|---|---|
| `enum_key` | string | PK. |
| `clr_full_name` | string | |
| `assembly_name` | string | |
| `description` | string, nullable | |
| `group_name` | string, nullable | |
| `manifest_hash` | string | Hash of the whole enum's member set, used to short-circuit a no-op sync pass for enums that did not change at all. |
| `last_sync_at` | timestamp | Updated every time this enum participates in a synchronization pass, whether or not any member row changed. |

### `SynchronizationOutcome` (in-memory, `Core`, not persisted)

The result of the diff engine comparing `DataDictionaryManifest` against the current
store state for one enum.

| Field | Type | Notes |
|---|---|---|
| `ToInsert` | `IReadOnlyList<DictionaryEntry>` | Members present in the manifest, absent from the store. |
| `ToUpdate` | `IReadOnlyList<DictionaryEntry>` | Members present in both, with a changed compared field. |
| `ToDeactivate` | `IReadOnlyList<DictionaryEntry>` | Stored, active, `field_name` absent from the manifest, code confirmed not in use. |
| `BreakingChanges` | `IReadOnlyList<BreakingChange>` | Stored, active, `field_name` absent from the manifest AND code in use (FR-018); or a `code` change colliding with an existing `field_name` (FR-019). Never auto-applied; routed through `OnBreakingChange` policy. |
| `Unchanged` | `IReadOnlyList<DictionaryEntry>` | Present in both, no compared field differs — recorded for observability/logging only; never written. |

`BreakingChange` carries everything FR-018's error message requires: enum key, field
name, code, and (when it is the in-use case) the identity of the referencing table —
obtained via `IsCodeInUseAsync`, see `contracts/store-contract.md`.

## Relationships

```
EnumDictionaryModel 1───* DictionaryMemberModel        (compile time)
DataDictionaryManifest 1───* ManifestEnumEntry           (compile time, generated)
DictionaryEnumCatalogEntry 1───* DictionaryEntry          (runtime, enum_key is the join key)
SynchronizationOutcome  ─── produced from ─── (DataDictionaryManifest, current DictionaryEntry rows, IsCodeInUseAsync results)
```

`enum_key` is the stable join key across every layer: it is assigned at the compile-time
model, carried into the generated manifest unchanged, and is half of the persisted
natural primary key.
