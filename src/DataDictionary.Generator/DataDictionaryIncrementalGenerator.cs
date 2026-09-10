using System.Collections.Immutable;
using DataDictionary.Generator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataDictionary.Generator;

/// <summary>
/// The compile-time entry point of the whole feature: an <see cref="IIncrementalGenerator"/>
/// that finds every dictionary-eligible enum (explicit mode via <c>[DataDictionary]</c>,
/// convention mode via <c>[assembly: DataDictionaryScan]</c>), validates it
/// (<see cref="DictionaryModelBuilder"/>, DD0001–DD0007), and emits the compile-time
/// <c>DataDictionaryManifest.Default</c> static (<see cref="ManifestEmitter"/>).
/// </summary>
/// <remarks>
/// Per <c>research.md</c> §3, registration is built on
/// <see cref="Microsoft.CodeAnalysis.SyntaxValueProvider.ForAttributeWithMetadataName"/>
/// for the three attribute shapes that name a specific syntax node directly —
/// <c>DataDictionaryAttribute</c> (explicit-mode enums),
/// <c>DataDictionaryDefaultsAttribute</c> and <c>DataDictionaryScanAttribute</c>
/// (assembly-level convention configuration). <c>DictionaryValueAttribute</c> is
/// deliberately not a fourth <c>ForAttributeWithMetadataName</c> registration — research.md
/// §3 lists only the three shapes above, and a member's own
/// <c>[DictionaryValue]</c> is read directly from its already-available
/// <see cref="IFieldSymbol"/> while resolving that enum's members
/// (<see cref="DictionaryModelBuilder"/>), which needs no separate incremental entry
/// point. A fourth, unattributed provider walks every enum declaration in the
/// compilation, because convention-mode enums are — by definition — not decorated with
/// any attribute an attribute-name provider could find; <see cref="DictionaryModelBuilder"/>
/// intersects that full enum list against the scanned namespace prefixes.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class DataDictionaryIncrementalGenerator : IIncrementalGenerator
{
    private const string DataDictionaryAttributeMetadataName = "DataDictionary.Abstractions.DataDictionaryAttribute";
    private const string DataDictionaryDefaultsAttributeMetadataName = "DataDictionary.Abstractions.DataDictionaryDefaultsAttribute";
    private const string DataDictionaryScanAttributeMetadataName = "DataDictionary.Abstractions.DataDictionaryScanAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var explicitEnums = context.SyntaxProvider.ForAttributeWithMetadataName(
                DataDictionaryAttributeMetadataName,
                predicate: static (node, _) => node is EnumDeclarationSyntax,
                transform: static (ctx, _) => new ExplicitEnumCandidate((INamedTypeSymbol)ctx.TargetSymbol, ctx.Attributes[0]))
            .Collect();

        var allEnums = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is EnumDeclarationSyntax,
                transform: static (ctx, _) => (INamedTypeSymbol)ctx.SemanticModel.GetDeclaredSymbol(ctx.Node)!)
            .Collect();

        var scanPrefixes = context.SyntaxProvider.ForAttributeWithMetadataName(
                DataDictionaryScanAttributeMetadataName,
                predicate: static (_, _) => true,
                transform: static (ctx, _) => ctx.Attributes)
            .Collect()
            .Select(static (attributeArrays, _) =>
            {
                var flattened = ImmutableArray.CreateBuilder<AttributeData>();
                foreach (var attributes in attributeArrays)
                {
                    flattened.AddRange(attributes);
                }

                return ConventionModeScanner.ReadScanPrefixes(flattened.ToImmutable());
            });

        var defaults = context.SyntaxProvider.ForAttributeWithMetadataName(
                DataDictionaryDefaultsAttributeMetadataName,
                predicate: static (_, _) => true,
                transform: static (ctx, _) => ctx.Attributes.Length > 0 ? (ConventionDefaults?)ConventionModeScanner.ReadDefaults(ctx.Attributes[0]) : null)
            .Collect()
            .Select(static (results, _) =>
            {
                foreach (var result in results)
                {
                    if (result is { } value)
                    {
                        return (ConventionDefaults?)value;
                    }
                }

                return null;
            });

        var combined = explicitEnums
            .Combine(allEnums)
            .Combine(scanPrefixes)
            .Combine(defaults)
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            var ((((explicitCandidates, everyEnum), scanNamespacePrefixes), conventionDefaults), compilation) = data;

            var (validEnums, diagnostics) = DictionaryModelBuilder.Build(
                compilation,
                explicitCandidates,
                everyEnum,
                scanNamespacePrefixes,
                conventionDefaults);

            foreach (var diagnostic in diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }

            var manifestSource = ManifestEmitter.Emit(compilation, validEnums);
            if (manifestSource is not null)
            {
                spc.AddSource(ManifestEmitter.HintName, manifestSource);
            }
        });
    }
}
