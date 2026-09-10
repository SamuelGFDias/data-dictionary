# Contract: generated entry points reaching `Core` at runtime

These members are not hand-written by the consumer; they are emitted by
`DataDictionary.Generator` into the consumer's own compiled assembly, and their shape is
still part of the public contract because consuming code (and `Core`'s DI wiring) calls
them by name.

## The generated manifest

```csharp
namespace <ConsumerAssembly>.Generated; // exact namespace finalized in implementation

public static class DataDictionaryManifest
{
    public static readonly DataDictionary.Abstractions.DataDictionaryManifest Default;
}
```

- Emitted once per compilation that contains at least one valid marked enum.
- `Default` is built entirely from compile-time-known literal data (see
  `data-model.md` — Compile-time models); no reflection, no attribute inspection, no
  assembly scanning occurs when this static member is initialized.

## Wiring into `Core` / DI (`ModelBuilder` extension and manifest registration)

```csharp
services.AddDataDictionary(builder =>
    builder.AddManifest(DataDictionaryManifest.Default));

// EF Core provider, inside OnModelCreating:
modelBuilder.ApplyDataDictionary();
```

- `AddDataDictionary(Action<IDataDictionaryBuilder>)` — `Core`-owned DI extension method
  that registers the synchronization service, the diff engine, and the configured
  `IDataDictionaryStore` implementation.
- `IDataDictionaryBuilder.AddManifest(DataDictionaryManifest manifest)` — registers one
  compiled manifest (normally the generated `Default`; supporting more than one call
  allows a host process composed of multiple assemblies, each with its own generated
  manifest, to register all of them).
- `ModelBuilder.ApplyDataDictionary()` — generated (or `Core`-provided and
  generator-augmented; finalized in implementation) EF Core extension method that:
  1. Configures the entity mappings for `tb_dicionario_dados` and (if enabled)
     `tb_dicionario_enum`, honoring the configured naming convention/table names
     (FR-011).
  2. Applies the generated `ValueConverter` for every business entity property whose
     CLR type is a marked enum (FR-012), so a business entity's `RacaCor` property
     persists as the enum's underlying numeric value or code per configuration, with
     zero hand-written converter registration by the consumer.

## Compatibility note

The method names `AddDataDictionary`, `AddManifest`, and `ApplyDataDictionary`, and the
existence/shape of the generated `DataDictionaryManifest.Default` static, are part of the
public contract (they appear verbatim in the constitution-mandated README example). A
rename or signature change to any of them is a breaking change under Principle I.
