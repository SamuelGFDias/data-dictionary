using DataDictionary.Abstractions;

namespace Sample.Api.Enums;

/// <summary>
/// The constitution-mandated README example: race/color classification, synchronized
/// into the persisted data dictionary (<c>tb_dicionario_dados</c>) under the
/// <c>RacaCor</c> enum key. See <c>quickstart.md</c> Scenario A.
/// </summary>
[DataDictionary("RacaCor", Group = "Cadastro")]
public enum RacaCor
{
    /// <summary>Branca</summary>
    [DictionaryValue("B")]
    Branca = 1,

    /// <summary>Preta</summary>
    [DictionaryValue("P")]
    Preta = 2,

    /// <summary>Parda</summary>
    [DictionaryValue("PA")]
    Parda = 3,

    /// <summary>Amarela</summary>
    [DictionaryValue("AM")]
    Amarela = 4,

    /// <summary>Indígena</summary>
    [DictionaryValue("I")]
    Indigena = 5,
}
