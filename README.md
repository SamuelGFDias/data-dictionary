# DataDictionary

Enum-driven data dictionary sync for .NET / EF Core — o enum C# é a fonte da verdade, o banco segue.

---

## Português

### O que é

DataDictionary é uma biblioteca .NET (ainda não publicada no NuGet) que trata um enum C#
marcado como a fonte da verdade para os valores válidos de uma coluna de banco de dados.
Um source generator Roslyn lê os enums marcados em tempo de compilação e gera o manifesto
de todo enum/membro elegível, o mapeamento EF Core da tabela de dicionário genérica e os
conversores de valor que ligam propriedades enum das entidades de negócio aos seus códigos
persistidos. Na inicialização da aplicação, o manifesto compilado é comparado com o banco
real: mudanças não-destrutivas são aplicadas automaticamente, e qualquer divergência
destrutiva aborta o boot com uma mensagem acionável.

### Exemplo copiável

Este é o exemplo canônico do repositório, definido em
`samples/Sample.Api/Enums/RacaCor.cs`:

```csharp
[DataDictionary("RacaCor", Group = "Cadastro")]
public enum RacaCor
{
    [DictionaryValue("B")]
    Branca = 1,

    [DictionaryValue("P")]
    Preta = 2,

    [DictionaryValue("PA")]
    Parda = 3,

    [DictionaryValue("AM")]
    Amarela = 4,

    [DictionaryValue("I")]
    Indigena = 5,
}
```

Ao sincronizar este enum contra um banco vazio, o resultado esperado (Cenário A do
`quickstart.md`) inclui, entre outras linhas, esta na tabela de dicionário:

| enum_key | field_name | code | numeric_value | description |
|---|---|---|---|---|
| `RacaCor` | `Branca` | `B` | `1` | `Branca` |

### Como conectar (wiring)

No `Program.cs` do sample, o manifesto gerado é registrado e o store baseado em EF Core é
exposto via `IDataDictionaryStore`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer("Server=localhost;Database=SampleApi;Trusted_Connection=True;"));

builder.Services.AddDataDictionary(b => b
    .AddManifest(Sample.Api.Generated.DataDictionaryManifest.Default)
    .WithSyncMode(SyncMode.Sync));

builder.Services.AddScoped<IDataDictionaryStore>(sp =>
    new EfDataDictionaryStore(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<DataDictionaryOptions>()));
```

No `AppDbContext`, o mapeamento das tabelas do dicionário (`tb_dicionario_dados` e,
opcionalmente, `tb_dicionario_enum`) entra no modelo via `ApplyDataDictionary()`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyDataDictionary();
}
```

### Arquitetura

O repositório é dividido em quatro projetos de biblioteca mais um sample:

- **`DataDictionary.Abstractions`** (`netstandard2.0`) — atributos públicos (`DataDictionary`,
  `DictionaryValue`), interfaces e tipos de configuração; é a superfície pública consumida
  por quem usa a biblioteca.
- **`DataDictionary.Generator`** (`netstandard2.0`) — source generator Roslyn incremental
  que lê os enums marcados e emite, em tempo de compilação, o manifesto, o mapeamento EF
  Core e os conversores de valor.
- **`DataDictionary.Core`** (`net10.0`) — motor de diff e orquestração de sincronização,
  sem nenhuma dependência de ORM; depende apenas da interface `IDataDictionaryStore`.
- **`DataDictionary.EntityFrameworkCore`** (`net10.0`) — implementação de
  `IDataDictionaryStore` via EF Core, com suporte a SQL Server e PostgreSQL.
- **`samples/Sample.Api`** — projeto de exemplo demonstrando o wiring completo descrito
  acima.

A separação rígida de target framework entre `Abstractions`/`Generator` (`netstandard2.0`)
e `Core`/`EntityFrameworkCore` (`net10.0`) é uma decisão deliberada, não acidental: ela é o
que permite ao generator rodar em qualquer host Roslyn, incluindo SDKs e IDEs mais antigos,
enquanto as bibliotecas de runtime usam recursos modernos do .NET.

### Status do projeto

O projeto está em desenvolvimento ativo sob GitHub Spec Kit, na feature
`001-enum-data-dictionary-sync`, e ainda não foi publicado no NuGet. Para o roadmap
detalhado e o escopo completo, consulte o Wiki do repositório:
[https://github.com/SamuelGFDias/data-dictionary/wiki](https://github.com/SamuelGFDias/data-dictionary/wiki).

### Escopo resumido

O que a v1/MVP cobre:

- Sincronização automática do dicionário na inicialização da aplicação.
- Fail-fast (abortar o boot) diante de qualquer divergência destrutiva.
- Sincronização incremental, tocando apenas nas entradas que mudaram.
- Retirada segura (desativação) de códigos que deixaram de existir no enum e não estão em uso.
- Boot seguro com múltiplas réplicas rodando simultaneamente.

O que está explicitamente fora de escopo nesta versão:

- Um provider baseado em Dapper.
- Exportação do dicionário para SQL ou CSV.
- Uma interface de linha de comando (CLI) para inspecionar ou gerenciar o dicionário.
- Suporte a enums `[Flags]` como fonte do dicionário.
- Geração automática de scripts de migration para o schema das próprias tabelas do dicionário.

O detalhamento completo das cinco user stories está no Wiki do repositório.

### Licença

A definir. Ainda não há um arquivo `LICENSE` publicado no repositório.

---

## English

### What it is

DataDictionary is a .NET library (not yet published to NuGet) that treats a marked C#
enum as the source of truth for a database column's valid values. A Roslyn source
generator reads the marked enums at compile time and emits a manifest of every eligible
enum/member, the EF Core mapping for a generic dictionary table, and the value converters
that link business-entity enum properties to their persisted codes. At application
startup, the compiled manifest is compared against the real database: non-destructive
changes are applied automatically, and any destructive divergence aborts the boot with an
actionable message.

### Copyable example

This is the repository's canonical example, defined in
`samples/Sample.Api/Enums/RacaCor.cs`:

```csharp
[DataDictionary("RacaCor", Group = "Cadastro")]
public enum RacaCor
{
    [DictionaryValue("B")]
    Branca = 1,

    [DictionaryValue("P")]
    Preta = 2,

    [DictionaryValue("PA")]
    Parda = 3,

    [DictionaryValue("AM")]
    Amarela = 4,

    [DictionaryValue("I")]
    Indigena = 5,
}
```

Syncing this enum against an empty database produces, among other rows, this row in the
dictionary table (Scenario A of `quickstart.md`):

| enum_key | field_name | code | numeric_value | description |
|---|---|---|---|---|
| `RacaCor` | `Branca` | `B` | `1` | `Branca` |

### How to wire it up

In the sample's `Program.cs`, the generated manifest is registered and the EF Core-backed
store is exposed via `IDataDictionaryStore`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer("Server=localhost;Database=SampleApi;Trusted_Connection=True;"));

builder.Services.AddDataDictionary(b => b
    .AddManifest(Sample.Api.Generated.DataDictionaryManifest.Default)
    .WithSyncMode(SyncMode.Sync));

builder.Services.AddScoped<IDataDictionaryStore>(sp =>
    new EfDataDictionaryStore(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<DataDictionaryOptions>()));
```

In `AppDbContext`, the dictionary's own tables (`tb_dicionario_dados` and, optionally,
`tb_dicionario_enum`) are mapped into the model via `ApplyDataDictionary()`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyDataDictionary();
}
```

### Architecture

The repository is split into four library projects plus a sample:

- **`DataDictionary.Abstractions`** (`netstandard2.0`) — public attributes
  (`DataDictionary`, `DictionaryValue`), interfaces, and configuration types; the public
  surface consumed by anyone using the library.
- **`DataDictionary.Generator`** (`netstandard2.0`) — an incremental Roslyn source
  generator that reads the marked enums and emits the manifest, EF Core mapping, and value
  converters at compile time.
- **`DataDictionary.Core`** (`net10.0`) — the diff/sync orchestration engine, with zero ORM
  dependencies; it depends only on the `IDataDictionaryStore` interface.
- **`DataDictionary.EntityFrameworkCore`** (`net10.0`) — an `IDataDictionaryStore`
  implementation over EF Core, supporting SQL Server and PostgreSQL.
- **`samples/Sample.Api`** — a sample project demonstrating the full wiring shown above.

The rigid target-framework split between `Abstractions`/`Generator` (`netstandard2.0`) and
`Core`/`EntityFrameworkCore` (`net10.0`) is a deliberate decision, not an accident: it is
what lets the generator run inside any Roslyn host, including older SDKs and IDEs, while
the runtime libraries use modern .NET features.

### Project status

The project is under active development using GitHub Spec Kit, on feature
`001-enum-data-dictionary-sync`, and has not yet been published to NuGet. For the detailed
roadmap and full scope, see the repository Wiki:
[https://github.com/SamuelGFDias/data-dictionary/wiki](https://github.com/SamuelGFDias/data-dictionary/wiki).

### Scope summary

What v1/MVP covers:

- Automatic dictionary synchronization at application startup.
- Fail-fast (boot aborts) on any destructive divergence.
- Incremental synchronization, touching only entries that actually changed.
- Safe retirement (deactivation) of codes that no longer exist in the enum and are unused.
- Safe boot with multiple replicas starting concurrently.

What is explicitly out of scope for this version:

- A Dapper-based provider.
- Exporting the dictionary to SQL or CSV.
- A command-line interface for inspecting or managing the dictionary.
- Support for `[Flags]` enums as a dictionary source.
- Automatic generation of migration scripts for the dictionary tables' own schema.

The full detail of the five user stories lives in the repository Wiki.

### License

To be defined. No `LICENSE` file has been published in the repository yet.
