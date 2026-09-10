using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Persistence;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core.Sync;
using Xunit;

namespace DataDictionary.Core.Tests;

/// <summary>
/// T049: an existing <c>field_name</c> whose manifest <c>code</c> changed to collide
/// with another stored, active <c>field_name</c>'s code must be classified into
/// <see cref="SynchronizationOutcome.BreakingChanges"/> with
/// <see cref="BreakingChangeReason.CodeCollision"/> — never auto-applied via
/// <see cref="SynchronizationOutcome.ToUpdate"/> or <see cref="SynchronizationOutcome.ToInsert"/>
/// (FR-019).
/// </summary>
public class DiffEngineCollisionTests
{
    [Fact]
    public async Task DiffAsync_MemberCodeChangedToCollideWithAnotherStoredField_ClassifiedAsBreakingChange()
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
                // "Branca"'s code changed from "B" to "P" in the manifest, colliding
                // with "Preta"'s already-stored, still-active code "P".
                new ManifestMemberEntry(
                    FieldName: "Branca",
                    Code: "P",
                    NumericValue: 1,
                    Description: "Cor branca.",
                    GroupName: null,
                    IsDeprecated: false,
                    SortOrder: 0),
                new ManifestMemberEntry(
                    FieldName: "Preta",
                    Code: "P",
                    NumericValue: 2,
                    Description: "Cor preta.",
                    GroupName: null,
                    IsDeprecated: false,
                    SortOrder: 1),
            ]);

        var currentState = new CurrentDictionaryState(
            EnumKey: "RacaCor",
            Entries:
            [
                new DictionaryEntry
                {
                    EnumKey = "RacaCor",
                    FieldName = "Branca",
                    Code = "B",
                    NumericValue = 1,
                    Description = "Cor branca.",
                    IsActive = true,
                    SortOrder = 0,
                },
                new DictionaryEntry
                {
                    EnumKey = "RacaCor",
                    FieldName = "Preta",
                    Code = "P",
                    NumericValue = 2,
                    Description = "Cor preta.",
                    IsActive = true,
                    SortOrder = 1,
                },
            ],
            CatalogEntry: null);

        // No member is removed from the manifest in this scenario, so
        // IsCodeInUseAsync must never be called — an empty lookup makes any
        // unexpected call fail loudly (see FakeDataDictionaryStore).
        var store = new FakeDataDictionaryStore(
            new Dictionary<(string, string), CodeUsageResult>());

        var engine = new DictionaryDiffEngine();

        var outcome = await engine.DiffAsync(manifestEnum, currentState, store, CancellationToken.None);

        var breakingChange = Assert.Single(outcome.BreakingChanges);
        Assert.Equal("RacaCor", breakingChange.EnumKey);
        Assert.Equal("Branca", breakingChange.FieldName);
        Assert.Equal("P", breakingChange.Code);
        Assert.Equal(BreakingChangeReason.CodeCollision, breakingChange.Reason);
        Assert.Equal(["Preta"], breakingChange.ReferencingTables);

        Assert.DoesNotContain(outcome.ToUpdate, entry => entry.FieldName == "Branca");
        Assert.DoesNotContain(outcome.ToInsert, entry => entry.FieldName == "Branca");
        Assert.Empty(outcome.ToDeactivate);
    }
}
