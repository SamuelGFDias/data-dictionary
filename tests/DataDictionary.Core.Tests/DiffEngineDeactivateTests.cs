using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Persistence;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core.Sync;
using Xunit;

namespace DataDictionary.Core.Tests;

/// <summary>
/// T062: a stored, active member whose <c>field_name</c> is absent from the manifest,
/// and whose code is confirmed unused, must be classified into
/// <see cref="SynchronizationOutcome.ToDeactivate"/> with <c>IsActive == false</c> —
/// the row is never deleted, only deactivated — and never into
/// <see cref="SynchronizationOutcome.BreakingChanges"/> (FR-018).
/// </summary>
public class DiffEngineDeactivateTests
{
    [Fact]
    public async Task DiffAsync_RemovedMemberCodeNotInUse_ClassifiedAsToDeactivate()
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
                [("RacaCor", "B")] = new CodeUsageResult(InUse: false, ReferencingTables: []),
            });

        var engine = new DictionaryDiffEngine();

        var outcome = await engine.DiffAsync(manifestEnum, currentState, store, CancellationToken.None);

        var deactivated = Assert.Single(outcome.ToDeactivate);
        Assert.Equal("RacaCor", deactivated.EnumKey);
        Assert.Equal("Branca", deactivated.FieldName);
        Assert.Equal("B", deactivated.Code);
        Assert.False(deactivated.IsActive);

        Assert.Empty(outcome.BreakingChanges);
        Assert.DoesNotContain(outcome.ToInsert, entry => entry.FieldName == "Branca");
    }
}
