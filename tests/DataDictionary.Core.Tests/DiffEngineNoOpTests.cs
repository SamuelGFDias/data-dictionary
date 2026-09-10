using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Persistence;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core.Sync;
using Xunit;

namespace DataDictionary.Core.Tests;

/// <summary>
/// T057: the diff engine's update-vs-unchanged classification — a member present in
/// both the manifest and the store is routed to <see cref="SynchronizationOutcome.Unchanged"/>
/// when its <c>content_hash</c> matches the stored row, or to
/// <see cref="SynchronizationOutcome.ToUpdate"/> when a compared field (description,
/// group, sort order, deprecated flag) differs — and never to
/// <see cref="SynchronizationOutcome.ToInsert"/> in either case.
/// </summary>
public class DiffEngineNoOpTests
{
    [Fact]
    public async Task DiffAsync_MatchingContentHash_EntryIsUnchanged()
    {
        var storedHash = DictionaryEntryHasher.Compute(
            description: "Cor branca.",
            groupName: null,
            sortOrder: 0,
            isDeprecated: false);

        var storedEntry = new DictionaryEntry
        {
            EnumKey = "RacaCor",
            FieldName = "Branca",
            Code = "B",
            NumericValue = 1,
            Description = "Cor branca.",
            GroupName = null,
            IsDeprecated = false,
            SortOrder = 0,
            IsActive = true,
            ContentHash = storedHash,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

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
            ]);

        var currentState = new CurrentDictionaryState(
            EnumKey: "RacaCor",
            Entries: [storedEntry],
            CatalogEntry: null);

        var engine = new DictionaryDiffEngine();
        var store = new FakeDataDictionaryStore(new Dictionary<(string, string), CodeUsageResult>());

        var outcome = await engine.DiffAsync(manifestEnum, currentState, store, CancellationToken.None);

        var unchanged = Assert.Single(outcome.Unchanged);
        Assert.Equal("Branca", unchanged.FieldName);
        Assert.Empty(outcome.ToInsert);
        Assert.Empty(outcome.ToUpdate);
        Assert.Empty(outcome.BreakingChanges);
    }

    [Fact]
    public async Task DiffAsync_DifferingDescription_EntryIsUpdated()
    {
        var storedHash = DictionaryEntryHasher.Compute(
            description: "Cor branca (antiga).",
            groupName: null,
            sortOrder: 0,
            isDeprecated: false);

        var storedEntry = new DictionaryEntry
        {
            EnumKey = "RacaCor",
            FieldName = "Branca",
            Code = "B",
            NumericValue = 1,
            Description = "Cor branca (antiga).",
            GroupName = null,
            IsDeprecated = false,
            SortOrder = 0,
            IsActive = true,
            ContentHash = storedHash,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

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
                    Description: "Cor branca (nova).",
                    GroupName: null,
                    IsDeprecated: false,
                    SortOrder: 0),
            ]);

        var currentState = new CurrentDictionaryState(
            EnumKey: "RacaCor",
            Entries: [storedEntry],
            CatalogEntry: null);

        var engine = new DictionaryDiffEngine();
        var store = new FakeDataDictionaryStore(new Dictionary<(string, string), CodeUsageResult>());

        var outcome = await engine.DiffAsync(manifestEnum, currentState, store, CancellationToken.None);

        var updated = Assert.Single(outcome.ToUpdate);
        Assert.Equal("Cor branca (nova).", updated.Description);
        Assert.Equal(storedEntry.EnumKey, updated.EnumKey);
        Assert.Equal(storedEntry.FieldName, updated.FieldName);
        Assert.Equal(storedEntry.Code, updated.Code);
        Assert.Equal(storedEntry.NumericValue, updated.NumericValue);
        Assert.Equal(storedEntry.CreatedAt, updated.CreatedAt);
        Assert.NotEqual(storedHash, updated.ContentHash);
        Assert.Empty(outcome.ToInsert);
        Assert.Empty(outcome.Unchanged);
        Assert.Empty(outcome.BreakingChanges);
    }

    [Fact]
    public async Task DiffAsync_MixedManifest_ClassifiesInsertUpdateAndUnchangedSeparately()
    {
        var unchangedHash = DictionaryEntryHasher.Compute(
            description: "Cor preta.",
            groupName: null,
            sortOrder: 1,
            isDeprecated: false);

        var updatedHash = DictionaryEntryHasher.Compute(
            description: "Cor branca (antiga).",
            groupName: null,
            sortOrder: 0,
            isDeprecated: false);

        var storedUnchanged = new DictionaryEntry
        {
            EnumKey = "RacaCor",
            FieldName = "Preta",
            Code = "P",
            NumericValue = 2,
            Description = "Cor preta.",
            GroupName = null,
            IsDeprecated = false,
            SortOrder = 1,
            IsActive = true,
            ContentHash = unchangedHash,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        var storedUpdated = new DictionaryEntry
        {
            EnumKey = "RacaCor",
            FieldName = "Branca",
            Code = "B",
            NumericValue = 1,
            Description = "Cor branca (antiga).",
            GroupName = null,
            IsDeprecated = false,
            SortOrder = 0,
            IsActive = true,
            ContentHash = updatedHash,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

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
                    Description: "Cor branca (nova).",
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
            Entries: [storedUnchanged, storedUpdated],
            CatalogEntry: null);

        var engine = new DictionaryDiffEngine();
        var store = new FakeDataDictionaryStore(new Dictionary<(string, string), CodeUsageResult>());

        var outcome = await engine.DiffAsync(manifestEnum, currentState, store, CancellationToken.None);

        Assert.Single(outcome.ToInsert);
        Assert.Equal("Parda", outcome.ToInsert[0].FieldName);

        Assert.Single(outcome.ToUpdate);
        Assert.Equal("Branca", outcome.ToUpdate[0].FieldName);

        Assert.Single(outcome.Unchanged);
        Assert.Equal("Preta", outcome.Unchanged[0].FieldName);

        Assert.Empty(outcome.BreakingChanges);
    }
}
