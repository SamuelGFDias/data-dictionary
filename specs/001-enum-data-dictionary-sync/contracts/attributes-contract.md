# Contract: public attributes and convention configuration (`DataDictionary.Abstractions`)

These are the surfaces a consuming developer writes directly in their own source code.
They are the primary public API of the library and are covered by Constitution
Principle I (semver) and Principle IX (XML doc mandatory on every one of them).

## Per-enum / per-member attributes (explicit mode)

```csharp
[DataDictionary("RacaCor", Group = "Cadastro")]
public enum RacaCor
{
    [DictionaryValue("B", Deprecated = false)]
    Branca,
    // ...
}
```

- `DataDictionaryAttribute(string enumKey)` — applied to an `enum` declaration. `Group`
  is an optional named property.
- `DictionaryValueAttribute(string code)` — applied to an enum member. `Deprecated` is an
  optional named `bool` property, default `false`.

## Assembly-level convention mode (mass adoption, no per-member attribute)

```csharp
[assembly: DataDictionaryDefaults(
    CodeSource = CodeSource.MemberName,
    DescriptionFrom = DescriptionSource.XmlDoc,
    RequireDescription = true)]
[assembly: DataDictionaryScan("MinhaApp.Domain.Enums")]
```

- `DataDictionaryDefaultsAttribute` — assembly-level. Named properties: `CodeSource`
  (enum: at minimum `MemberName`; other sources are a natural extension point but
  `MemberName` is the only value exercised by the spec's examples), `DescriptionFrom`
  (enum: `XmlDoc`, `DescriptionAttribute`, `DisplayAttribute`, `MemberName` — mirrors the
  precedence order in FR-005), `RequireDescription` (`bool`).
- `DataDictionaryScanAttribute(string namespacePrefix)` — assembly-level, repeatable
  (multiple scanned namespaces = multiple attribute applications). Every enum inside a
  scanned namespace is treated as dictionary-eligible without an explicit
  `[DataDictionary]` attribute; its `enumKey` defaults to the enum's simple name unless
  a `[DataDictionary]` attribute is still present to override it (explicit attributes on
  a scanned enum are additive/overriding, not conflicting).

## Description precedence (FR-005), for both modes

1. The member's XML documentation `<summary>`.
2. `[Description]` (`System.ComponentModel.DescriptionAttribute`).
3. `[Display(Name = ...)]` (`System.ComponentModel.DataAnnotations.DisplayAttribute`).
4. The member's own C# name, as the final fallback.

## Compatibility note

Attribute constructor signatures, named-property names/types, and enum member names on
`CodeSource`/`DescriptionSource` are all part of the public contract. A change to any of
them (renaming a property, changing a parameter's type, adding a required constructor
parameter) is a breaking change under Constitution Principle I and requires a MAJOR
version bump.
