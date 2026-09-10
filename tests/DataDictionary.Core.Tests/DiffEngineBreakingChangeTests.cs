using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Persistence;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core.Sync;
using Xunit;

namespace DataDictionary.Core.Tests;

/// <summary>
/// T048: a stored, active member whose <c>field_name</c> is absent from the manifest,
/// and whose code is still referenced by business data, must be classified into
/// <see cref="SynchronizationOutcome.BreakingChanges"/> with
/// <see cref="BreakingChangeReason.InUseCodeRemoved"/> — never into
/// <see cref="SynchronizationOutcome.ToDeactivate"/> (FR-018).
/// </summary>
public class DiffEngineBreakingChangeTests
{
    [Fact]
    public async Task DiffAsync_RemovedMemberCodeInUse_ClassifiedAsBreakingChange_NeverToDeactivate()
    {
        var manifestEnum = new ManifestEnumEntry(
            EnumKey: "RacaCor",
            GroupName: null,
            ClrFullName: "Fixture.RacaCor",
            AssemblyName: "Fixture.Assembly",
            Description: "Raça/cor declarada pelo cidadão.",
            IsFlags: false,
            Members:
            [
                // "Branca" existed in the store but was removed from the manifest —
                // only "Preta" remains.
                new ManifestMemberEntry(
                    FieldName: "Preta",
                    Code: "P",
                    NumericValue: 2,
                    Description: "Cor preta.",
                    GroupName: null,
                    IsDeprecated: false,
                    SortOrder: 0),
            ]);

        var removedEntry = new DictionaryEntry
        {
            EnumKey = "RacaCor",
            FieldName = "Branca",
            Code = "B",
            NumericValue = 1,
            Description = "Cor branca.",
            IsActive = true,
            SortOrder = 0,
        };

        var pretaEntry = new DictionaryEntry
        {
            EnumKey = "RacaCor",
            FieldName = "Preta",
            Code = "P",
            NumericValue = 2,
            Description = "Cor preta.",
            IsActive = true,
            SortOrder = 1,
        };

        var currentState = new CurrentDictionaryState(
            EnumKey: "RacaCor",
            Entries: [removedEntry, pretaEntry],
            CatalogEntry: null);

        var store = new FakeDataDictionaryStore(
            new Dictionary<(string, string), CodeUsageResult>
            {
                [("RacaCor", "B")] = new CodeUsageResult(InUse: true, ReferencingTables: ["tb_cidadao"]),
            });

        var engine = new DictionaryDiffEngine();

        var outcome = await engine.DiffAsync(manifestEnum, currentState, store, CancellationToken.None);

        var breakingChange = Assert.Single(outcome.BreakingChanges);
        Assert.Equal("RacaCor", breakingChange.EnumKey);
        Assert.Equal("Branca", breakingChange.FieldName);
        Assert.Equal("B", breakingChange.Code);
        Assert.Equal(BreakingChangeReason.InUseCodeRemoved, breakingChange.Reason);
        Assert.Equal(["tb_cidadao"], breakingChange.ReferencingTables);

        Assert.DoesNotContain(outcome.ToDeactivate, entry => entry.FieldName == "Branca");
        Assert.Empty(outcome.ToInsert);
    }
}
