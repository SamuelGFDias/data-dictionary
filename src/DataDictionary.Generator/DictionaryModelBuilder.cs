using System.Collections.Immutable;
using DataDictionary.Generator.Diagnostics;
using DataDictionary.Generator.Internal;
using DataDictionary.Generator.Model;
using Microsoft.CodeAnalysis;

namespace DataDictionary.Generator;

/// <summary>
/// Joins the explicit-mode candidates (<c>[DataDictionary]</c>-decorated enums) and the
/// convention-mode candidates (enums reached through
/// <c>[assembly: DataDictionaryScan]</c>) into one deduplicated set, resolves every
/// enum's and member's fields, runs every DD0001–DD0007 validation rule, and returns the
/// final set of enums that pass validation together with every diagnostic to report.
/// </summary>
/// <remarks>
/// This is the single point where the whole-compilation view every uniqueness check
/// (DD0002, DD0005) needs is available, so — deliberately — this stage recomputes
/// whenever any of its collected inputs change, rather than trying to validate each
/// enum in isolation. Every input and output is still an immutable, structurally
/// equatable value (<c>research.md</c> §3), so the incremental cache still short-circuits
/// correctly when nothing relevant changed; this is a compilation-granularity
/// incrementality trade-off, not an abandonment of the equatable-model requirement.
/// </remarks>
internal static class DictionaryModelBuilder
{
    private const string DictionaryValueAttributeMetadataName = "DataDictionary.Abstractions.DictionaryValueAttribute";
    private const string FlagsAttributeMetadataName = "System.FlagsAttribute";
    private const string DeprecatedPropertyName = "Deprecated";

    internal static (ImmutableArray<GeneratorEnumModel> Enums, ImmutableArray<Diagnostic> Diagnostics) Build(
        Compilation compilation,
        ImmutableArray<ExplicitEnumCandidate> explicitCandidates,
        ImmutableArray<INamedTypeSymbol> allEnums,
        ImmutableArray<string> scanPrefixes,
        ConventionDefaults? defaults)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var assemblyName = compilation.AssemblyName ?? string.Empty;
        var effectiveDefaults = defaults ?? ConventionDefaults.Default;

        // Deduplicate: an enum may be both explicitly [DataDictionary]-marked AND fall
        // inside a scanned namespace (explicit attributes on a scanned enum are
        // additive/overriding, not conflicting — attributes-contract.md). Key by symbol
        // so each candidate enum is analyzed exactly once.
        var explicitBySymbol = new Dictionary<INamedTypeSymbol, AttributeData>(SymbolEqualityComparer.Default);
        foreach (var candidate in explicitCandidates)
        {
            // AllowMultiple = false on the source attribute; a duplicate key here would
            // mean the same syntax node was visited twice, which ForAttributeWithMetadataName
            // does not do. Last-write-wins is a safe, simple guard either way.
            explicitBySymbol[candidate.Symbol] = candidate.Attribute;
        }

        var scannedSymbols = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        if (!scanPrefixes.IsDefaultOrEmpty)
        {
            foreach (var enumSymbol in allEnums)
            {
                if (ConventionModeScanner.IsInScannedNamespace(enumSymbol, scanPrefixes))
                {
                    scannedSymbols.Add(enumSymbol);
                }
            }
        }

        var candidateSymbols = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var symbol in explicitBySymbol.Keys)
        {
            candidateSymbols.Add(symbol);
        }

        foreach (var symbol in scannedSymbols)
        {
            candidateSymbols.Add(symbol);
        }

        var builtEnums = ImmutableArray.CreateBuilder<GeneratorEnumModel>();

        foreach (var enumSymbol in candidateSymbols.OrderBy(s => s.ToDisplayString(), StringComparer.Ordinal))
        {
            var isConventionGoverned = scannedSymbols.Contains(enumSymbol);
            var hasExplicitAttribute = explicitBySymbol.TryGetValue(enumSymbol, out var explicitAttribute);
            var enumLocation = enumSymbol.Locations.FirstOrDefault() ?? Location.None;

            // DD0007: a [Flags] enum can never be a dictionary source, and is excluded
            // entirely rather than producing a (necessarily meaningless) model.
            if (HasFlagsAttribute(enumSymbol))
            {
                diagnostics.Add(DD0007.Create(enumLocation, enumSymbol.Name));
                continue;
            }

            var enumKey = ResolveEnumKey(enumSymbol, hasExplicitAttribute, explicitAttribute);
            var group = hasExplicitAttribute ? GetNamedStringArgument(explicitAttribute!, "Group") : null;
            // DD0004's contract text scopes it to "a member" only, so an unresolved
            // enum-level description (only reachable under the same RequireDescription
            // suppression as members) is left null here without a diagnostic.
            var allowEnumNameFallback = !(isConventionGoverned && effectiveDefaults.RequireDescription);
            var description = DescriptionResolver.Resolve(enumSymbol, allowEnumNameFallback);

            var members = BuildMembers(
                enumSymbol,
                isConventionGoverned,
                effectiveDefaults,
                group,
                diagnostics);

            builtEnums.Add(new GeneratorEnumModel(
                EnumKey: enumKey,
                GroupName: group,
                ClrFullName: enumSymbol.ToDisplayString(),
                AssemblyName: assemblyName,
                Description: description,
                IsFlags: false,
                Members: members,
                Location: enumLocation));
        }

        var validatedEnums = ValidateEnumKeys(builtEnums.ToImmutable(), diagnostics);

        return (validatedEnums, diagnostics.ToImmutable());
    }

    private static string ResolveEnumKey(INamedTypeSymbol enumSymbol, bool hasExplicitAttribute, AttributeData? explicitAttribute)
    {
        if (hasExplicitAttribute
            && explicitAttribute!.ConstructorArguments.Length > 0
            && explicitAttribute.ConstructorArguments[0].Value is string explicitKey
            && !string.IsNullOrEmpty(explicitKey))
        {
            return explicitKey;
        }

        // Convention default (also the fallback for an explicit attribute constructed
        // with an empty string — DD0005's "must be non-empty" rule catches that case
        // downstream rather than silently substituting the simple name for it).
        return hasExplicitAttribute ? string.Empty : enumSymbol.Name;
    }

    private static ImmutableArray<GeneratorMemberModel> BuildMembers(
        INamedTypeSymbol enumSymbol,
        bool isConventionGoverned,
        ConventionDefaults defaults,
        string? enumGroup,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var resolvedMembers = ImmutableArray.CreateBuilder<GeneratorMemberModel>();
        var sortOrder = 0;

        foreach (var field in enumSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            // Enum member fields always have a constant value; the compiler-generated
            // `value__` backing field does not, which is how it is excluded here.
            if (field.ConstantValue is null)
            {
                continue;
            }

            var memberLocation = field.Locations.FirstOrDefault() ?? Location.None;
            var dictionaryValueAttribute = field.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DictionaryValueAttributeMetadataName);

            string? code;
            GeneratorCodeSource codeSource;
            var isDeprecated = false;

            if (dictionaryValueAttribute is not null)
            {
                code = dictionaryValueAttribute.ConstructorArguments.Length > 0
                    ? dictionaryValueAttribute.ConstructorArguments[0].Value as string
                    : null;
                codeSource = GeneratorCodeSource.Explicit;
                isDeprecated = GetNamedBoolArgument(dictionaryValueAttribute, DeprecatedPropertyName);
            }
            else if (isConventionGoverned)
            {
                code = CodeResolver.ResolveFromConventionSource(field, defaults.CodeSource);
                codeSource = defaults.CodeSource;
            }
            else
            {
                // Explicit mode, no [DictionaryValue]: there is no fallback — DD0001
                // fires unconditionally for this member.
                code = null;
                codeSource = GeneratorCodeSource.Explicit;
            }

            var allowMemberNameFallback = !(isConventionGoverned && defaults.RequireDescription);
            var description = DescriptionResolver.Resolve(field, allowMemberNameFallback);

            resolvedMembers.Add(new GeneratorMemberModel(
                FieldName: field.Name,
                Code: string.IsNullOrEmpty(code) ? null : code,
                NumericValue: EnumValueWidener.ToInt64(field.ConstantValue),
                Description: description,
                GroupName: enumGroup,
                IsDeprecated: isDeprecated,
                SortOrder: sortOrder,
                CodeSource: codeSource,
                Location: memberLocation));

            sortOrder++;
        }

        return ValidateMembers(enumSymbol.Name, resolvedMembers.ToImmutable(), defaults.MaxCodeLength, diagnostics);
    }

    private static ImmutableArray<GeneratorMemberModel> ValidateMembers(
        string enumName,
        ImmutableArray<GeneratorMemberModel> members,
        int maxCodeLength,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var survivors = ImmutableArray.CreateBuilder<GeneratorMemberModel>();

        // DD0001 (unresolved code) and DD0003 (code too long) both exclude the member
        // outright; evaluate them before the DD0002 duplicate-code grouping so a
        // member excluded for either reason never participates in that grouping.
        foreach (var member in members)
        {
            if (member.Code is null)
            {
                diagnostics.Add(DD0001.Create(member.Location, enumName, member.FieldName));
                continue;
            }

            if (member.Code.Length > maxCodeLength)
            {
                diagnostics.Add(DD0003.Create(member.Location, enumName, member.FieldName, member.Code, maxCodeLength));
                continue;
            }

            survivors.Add(member);
        }

        // DD0002: duplicate code within the enum. Keep the first (by declaration
        // order/SortOrder) member of each colliding code, exclude the rest.
        var codeGroups = new Dictionary<string, GeneratorMemberModel>(StringComparer.Ordinal);
        var deduplicated = ImmutableArray.CreateBuilder<GeneratorMemberModel>();

        foreach (var member in survivors.OrderBy(m => m.SortOrder))
        {
            if (codeGroups.TryGetValue(member.Code!, out var first))
            {
                diagnostics.Add(DD0002.Create(member.Location, enumName, member.FieldName, member.Code!, first.FieldName));
                continue;
            }

            codeGroups[member.Code!] = member;
            deduplicated.Add(member);
        }

        // DD0006 (Warning, non-exclusionary): a numeric alias among the members that
        // otherwise survived validation.
        var numericGroups = new Dictionary<long, GeneratorMemberModel>();
        foreach (var member in deduplicated)
        {
            if (numericGroups.TryGetValue(member.NumericValue, out var first))
            {
                diagnostics.Add(DD0006.Create(member.Location, enumName, member.FieldName, member.NumericValue, first.FieldName));
                continue;
            }

            numericGroups[member.NumericValue] = member;
        }

        // DD0004 (Warning, non-exclusionary): report for every surviving member whose
        // description genuinely did not resolve (only reachable when RequireDescription
        // suppressed the member-name fallback — see DescriptionResolver's remarks).
        foreach (var member in deduplicated)
        {
            if (member.Description is null)
            {
                diagnostics.Add(DD0004.Create(member.Location, enumName, member.FieldName));
            }
        }

        return deduplicated.ToImmutable();
    }

    private static ImmutableArray<GeneratorEnumModel> ValidateEnumKeys(
        ImmutableArray<GeneratorEnumModel> enums,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var result = ImmutableArray.CreateBuilder<GeneratorEnumModel>();
        var keyGroups = new Dictionary<string, GeneratorEnumModel>(StringComparer.Ordinal);
        var excludedForDuplicateKey = new HashSet<string>(StringComparer.Ordinal);

        foreach (var enumModel in enums)
        {
            if (string.IsNullOrWhiteSpace(enumModel.EnumKey))
            {
                diagnostics.Add(DD0005.CreateEmptyKey(enumModel.Location, enumModel.ClrFullName));
                continue;
            }

            if (keyGroups.TryGetValue(enumModel.EnumKey, out var first))
            {
                diagnostics.Add(DD0005.CreateDuplicateKey(enumModel.Location, enumModel.ClrFullName, enumModel.EnumKey, first.ClrFullName));
                diagnostics.Add(DD0005.CreateDuplicateKey(first.Location, first.ClrFullName, first.EnumKey, enumModel.ClrFullName));
                excludedForDuplicateKey.Add(enumModel.EnumKey);
                continue;
            }

            keyGroups[enumModel.EnumKey] = enumModel;
        }

        foreach (var enumModel in enums)
        {
            if (string.IsNullOrWhiteSpace(enumModel.EnumKey))
            {
                continue;
            }

            if (excludedForDuplicateKey.Contains(enumModel.EnumKey))
            {
                continue;
            }

            result.Add(enumModel);
        }

        return result.ToImmutable();
    }

    private static bool HasFlagsAttribute(INamedTypeSymbol enumSymbol) =>
        enumSymbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == FlagsAttributeMetadataName);

    private static string? GetNamedStringArgument(AttributeData attribute, string name)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is string value)
            {
                return value;
            }
        }

        return null;
    }

    private static bool GetNamedBoolArgument(AttributeData attribute, string name)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is bool value)
            {
                return value;
            }
        }

        return false;
    }
}
