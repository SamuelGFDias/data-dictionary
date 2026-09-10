using DataDictionary.Abstractions.Configuration;
using DataDictionary.Abstractions.Manifest;
using DataDictionary.Abstractions.Sync;
using DataDictionary.Core;
using DataDictionary.Core.Sync;
using Xunit;

namespace DataDictionary.Core.Tests;

/// <summary>
/// Covers <c>spec.md</c> User Story 2, Acceptance Scenario 3: under
/// <see cref="SyncMode.ValidateOnly"/>, the boot must fail on ANY divergence between the
/// manifest and the stored dictionary — not only breaking changes — and nothing must be
/// written. Fixes the gap left by <c>tasks.md</c> T056's literal wording (breaking-change
/// policy only), which the spec's acceptance scenario overrides.
/// </summary>
public class DataDictionarySynchronizerValidateOnlyTests
{
    [Fact]
    public async Task SynchronizeAsync_ValidateOnly_InsertOnlyDivergence_ThrowsValidationException_NeverApplies()
    {
        // "Nova" is a brand-new member — present in the manifest, absent from the
        // store — so DiffAsync classifies it as a plain ToInsert with no breaking
        // change at all.
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
                    FieldName: "Nova",
                    Code: "N",
                    NumericValue: 1,
                    Description: "Nova entrada.",
                    GroupName: null,
                    IsDeprecated: false,
                    SortOrder: 0),
            ]);

        var currentState = new CurrentDictionaryState(
            EnumKey: "RacaCor",
            Entries: [],
            CatalogEntry: null);

        var store = new FakeDataDictionaryStore(
            codeUsageResults: new Dictionary<(string, string), CodeUsageResult>(),
            currentStates: new Dictionary<string, CurrentDictionaryState>
            {
                ["RacaCor"] = currentState,
            });

        var options = new DataDictionaryOptions(
            Manifests: [new DataDictionaryManifest([manifestEnum])],
            SyncMode: SyncMode.ValidateOnly,
            OnBreakingChange: OnBreakingChange.Fail);

        var synchronizer = new DataDictionarySynchronizer(
            options,
            store,
            new DictionaryDiffEngine(),
            new BreakingChangePolicyEngine());

        var exception = await Assert.ThrowsAsync<DataDictionaryValidationException>(
            () => synchronizer.SynchronizeAsync(CancellationToken.None));

        Assert.Equal("RacaCor", exception.EnumKey);
        Assert.Single(exception.Outcome.ToInsert);
        Assert.Empty(exception.Outcome.ToUpdate);
        Assert.Empty(exception.Outcome.ToDeactivate);
        Assert.Empty(exception.Outcome.BreakingChanges);
        Assert.Contains("RacaCor", exception.Message, StringComparison.Ordinal);
        Assert.Contains("1 to insert", exception.Message, StringComparison.Ordinal);

        Assert.False(store.ApplyAsyncCalled);
        Assert.Equal(0, store.ApplyAsyncCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_ValidateOnly_NoDivergence_DoesNotThrow_NeverApplies()
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
                    FieldName: "Preta",
                    Code: "P",
                    NumericValue: 1,
                    Description: "Cor preta.",
                    GroupName: null,
                    IsDeprecated: false,
                    SortOrder: 0),
            ]);

        var currentState = new CurrentDictionaryState(
            EnumKey: "RacaCor",
            Entries:
            [
                new Abstractions.Persistence.DictionaryEntry
                {
                    EnumKey = "RacaCor",
                    FieldName = "Preta",
                    Code = "P",
                    NumericValue = 1,
                    Description = "Cor preta.",
                    IsActive = true,
                    SortOrder = 0,
                    ContentHash = DictionaryEntryHasher.Compute("Cor preta.", null, 0, false),
                },
            ],
            CatalogEntry: null);

        var store = new FakeDataDictionaryStore(
            codeUsageResults: new Dictionary<(string, string), CodeUsageResult>(),
            currentStates: new Dictionary<string, CurrentDictionaryState>
            {
                ["RacaCor"] = currentState,
            });

        var options = new DataDictionaryOptions(
            Manifests: [new DataDictionaryManifest([manifestEnum])],
            SyncMode: SyncMode.ValidateOnly,
            OnBreakingChange: OnBreakingChange.Fail);

        var synchronizer = new DataDictionarySynchronizer(
            options,
            store,
            new DictionaryDiffEngine(),
            new BreakingChangePolicyEngine());

        await synchronizer.SynchronizeAsync(CancellationToken.None);

        Assert.False(store.ApplyAsyncCalled);
    }
}
