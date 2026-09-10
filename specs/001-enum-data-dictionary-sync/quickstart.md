# Quickstart: validating the enum-sourced data dictionary sync

This is a validation/run guide, not an implementation guide. It documents the scenarios
that prove the feature works end-to-end, referencing `contracts/` and `data-model.md`
instead of duplicating their content. No full implementation code, entity/service
bodies, or migrations are included here — those belong to the implementation phase.

## Prerequisites

- .NET 10 SDK installed.
- A disposable SQL Server or PostgreSQL instance reachable from the test/sample host
  (locally, this is exactly what the Testcontainers-based integration tests spin up —
  see `research.md` §9; the same containers can be run manually for exploratory
  validation).
- The solution's `samples/Sample.Api` project (see `plan.md` — Project Structure),
  referencing `DataDictionary.EntityFrameworkCore`.

## Scenario A — canonical example (README-mandated)

1. Declare, in `samples/Sample.Api`, the enum from the constitution-mandated README
   example:

   ```csharp
   [DataDictionary("RacaCor", Group = "Cadastro")]
   public enum RacaCor
   {
       /// <summary>Branca</summary>
       [DictionaryValue("B")]
       Branca = 1,
       // ...
   }
   ```

2. Wire it per `contracts/generated-entrypoints-contract.md`:
   `services.AddDataDictionary(b => b.AddManifest(DataDictionaryManifest.Default))` and
   `modelBuilder.ApplyDataDictionary()`.
3. Set the synchronization mode to `Sync` explicitly (the clarified default is `Off` —
   see `spec.md` `## Clarifications` — so an explicit opt-in is required for this
   scenario).
4. Start `Sample.Api` against an empty database.
5. **Expected outcome** (this is the row the README's copyable example shows):

   | enum_key | field_name | code | numeric_value | description |
   |---|---|---|---|---|
   | `RacaCor` | `Branca` | `B` | `1` | `Branca` |

   Matches `spec.md` Success Criterion SC-001 and User Story 1's acceptance scenario 1.

## Scenario B — idempotent incremental sync

1. Starting from Scenario A's state, add one new member to `RacaCor` in source.
2. Restart `Sample.Api` (still `Sync` mode).
3. **Expected outcome**: exactly one new row appears; every pre-existing row's
   `updated_at` is unchanged. Matches SC-002 and User Story 3's acceptance scenarios.

## Scenario C — fail-fast on in-use code removal

1. Starting from Scenario A's state, insert a row into a business table whose column is
   mapped as `RacaCor` (via the generated `ValueConverter`) with the value `Branca`.
2. Remove the `Branca` member from the enum in source.
3. Restart `Sample.Api` (`Sync` mode).
4. **Expected outcome**: startup fails; the error names `RacaCor`, `Branca`, `B`, and the
   business table from step 1. Matches SC-003 and User Story 2's acceptance scenario 1.

## Scenario D — retirement of an unused code

1. Same as Scenario C but skip step 1 (no business row ever used `Branca`'s code).
2. Remove `Branca`, restart in `Sync` mode.
3. **Expected outcome**: the `RacaCor`/`Branca` row's `is_active` becomes `false`; boot
   succeeds. Matches User Story 4's acceptance scenario.

## Scenario E — `ValidateOnly` writes nothing

1. Starting from Scenario A's state, introduce any divergence (e.g. Scenario B's added
   member, not yet synced).
2. Set mode to `ValidateOnly`, restart.
3. **Expected outcome**: startup fails (divergence found under the default `Fail`
   breaking-change policy — see `spec.md` Clarifications), and the dictionary table is
   byte-for-byte unchanged from before the attempt. Matches SC-004.

## Scenario F — concurrent replica boot

1. Starting from an empty database, start two instances of `Sample.Api` at
   (approximately) the same moment, both in `Sync` mode.
2. **Expected outcome**: both instances finish starting successfully; the dictionary ends
   up in the same state as Scenario A; no unique-constraint violation occurs; exactly one
   instance actually performed the write (observable via the sample's own startup log,
   or by asserting only one instance's process ever entered the write path in the
   integration test). Matches SC-005 and User Story 5.

## Scenario G — build-time diagnostics

1. Add a second member to `RacaCor` with the same code as an existing one (`"B"`).
2. Build the solution.
3. **Expected outcome**: the build fails with `DD0002`, naming both colliding members.
   Matches SC-006 and the spec's final acceptance scenario.

## Running these as automated tests

- Scenarios A–F correspond to `DataDictionary.EntityFrameworkCore.Tests` integration
  tests (Testcontainers-backed, one test class per scenario is a reasonable split).
- Scenario G corresponds to a `DataDictionary.Generator.Tests` snapshot test asserting
  the `DD0002` diagnostic is produced for the fixture source.
- The diff-only logic behind Scenarios B–E (insert/update/deactivate/abort
  classification) is additionally covered at the unit level in
  `DataDictionary.Core.Tests`, without a real database, per `research.md` §9.
