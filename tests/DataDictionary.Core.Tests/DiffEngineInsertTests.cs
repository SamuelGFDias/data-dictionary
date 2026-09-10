using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core.Sync;
using Xunit;

namespace DataDictionary.Core.Tests;

/// <summary>
/// T041: the diff engine's insert-only classification — an empty
/// <see cref="CurrentDictionaryState"/> against a full manifest must put every member in
/// <see cref="SynchronizationOutcome.ToInsert"/> and nothing in
/// <see cref="SynchronizationOutcome.ToUpdate"/>,
/// <see cref="SynchronizationOutcome.ToDeactivate"/>, or
/// <see cref="SynchronizationOutcome.BreakingChanges"/>.
/// </summary>
public class DiffEngineInsertTests
{
    [Fact]
    public async Task DiffAsync_EmptyCurrentState_FullManifest_EverythingIsInserted()
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
                new ManifestMemberEntry(
                    FieldName: "Branca",
                    Code: "B",
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
                new ManifestMemberEntry(
                    FieldName: "Parda",
                    Code: "PA",
                    NumericValue: 3,
                    Description: "Cor parda.",
                    GroupName: null,
                    IsDeprecated: false,
                    SortOrder: 2),
            ]);

        var currentState = new CurrentDictionaryState(
            EnumKey: "RacaCor",
            Entries: [],
            CatalogEntry: null);

        var engine = new DictionaryDiffEngine();
        var store = new FakeDataDictionaryStore(new Dictionary<(string, string), CodeUsageResult>());

        var outcome = await engine.DiffAsync(manifestEnum, currentState, store, CancellationToken.None);

        Assert.Equal(manifestEnum.Members.Length, outcome.ToInsert.Count);

        foreach (var member in manifestEnum.Members)
        {
            var inserted = Assert.Single(
                outcome.ToInsert,
                entry => entry.FieldName == member.FieldName);

            Assert.Equal(manifestEnum.EnumKey, inserted.EnumKey);
            Assert.Equal(member.Code, inserted.Code);
            Assert.Equal(member.NumericValue, inserted.NumericValue);
            Assert.Equal(member.Description, inserted.Description);
            Assert.Equal(member.GroupName, inserted.GroupName);
            Assert.Equal(member.IsDeprecated, inserted.IsDeprecated);
            Assert.Equal(member.SortOrder, inserted.SortOrder);
            Assert.True(inserted.IsActive);
        }

        Assert.Empty(outcome.ToUpdate);
        Assert.Empty(outcome.ToDeactivate);
        Assert.Empty(outcome.BreakingChanges);
    }
}
