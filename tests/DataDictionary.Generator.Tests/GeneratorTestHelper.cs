using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using DataDictionary.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DataDictionary.Generator.Tests;

/// <summary>
/// Drives <see cref="DataDictionaryIncrementalGenerator"/> against fixture source text
/// and formats its output (generated sources + diagnostics) into a single deterministic
/// string, ready to hand to Verify for snapshotting — the approach
/// <c>research.md</c> §9 calls for ("Verify ... together with ...
/// Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing / Microsoft.CodeAnalysis.Testing
/// for driving the generator against fixture sources and snapshotting emitted output and
/// diagnostics"). It drives the generator directly via <see cref="CSharpGeneratorDriver"/>
/// rather than the packages' <c>CSharpSourceGeneratorTest&lt;TGenerator, TVerifier&gt;</c>
/// harness: that harness needs an <c>IVerifier</c> implementation
/// (<c>Microsoft.CodeAnalysis.Testing.Verifiers.XUnit</c>), which is not part of this
/// solution's referenced package set, and it is built for "expected fixed code" style
/// assertions rather than free-form snapshotting — a thin, purpose-built driver here is
/// both simpler and a closer fit for Verify-based snapshot testing.
/// </summary>
internal static class GeneratorTestHelper
{
    /// <summary>
    /// Compiles <paramref name="source"/> (plus any <paramref name="additionalSources"/>)
    /// against <c>DataDictionary.Abstractions</c> and every currently-loaded BCL
    /// reference assembly, runs <see cref="DataDictionaryIncrementalGenerator"/> against
    /// the result, and returns a single formatted string combining every generated
    /// source file and every reported diagnostic — deterministic and suitable for a
    /// Verify snapshot.
    /// </summary>
    internal static string Run(string source, params string[] additionalSources)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse);

        var syntaxTrees = new[] { source }
            .Concat(additionalSources)
            .Select(text => CSharpSyntaxTree.ParseText(text, parseOptions))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "DataDictionary.Generator.Tests.Fixture",
            syntaxTrees: syntaxTrees,
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: ImmutableArray.Create(new DataDictionaryIncrementalGenerator().AsSourceGenerator()),
            parseOptions: parseOptions);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var runResult = driver.GetRunResult();

        return Format(runResult);
    }

    private static string Format(GeneratorDriverRunResult runResult)
    {
        var builder = new StringBuilder();

        foreach (var result in runResult.Results)
        {
            foreach (var generatedSource in result.GeneratedSources.OrderBy(s => s.HintName, StringComparer.Ordinal))
            {
                builder.AppendLine("========== GENERATED: " + generatedSource.HintName + " ==========");
                builder.AppendLine(generatedSource.SourceText.ToString());
            }
        }

        var diagnostics = runResult.Diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.GetMessage(), StringComparer.Ordinal)
            .ToList();

        builder.AppendLine("========== DIAGNOSTICS ==========");

        if (diagnostics.Count == 0)
        {
            builder.AppendLine("(none)");
        }

        foreach (var diagnostic in diagnostics)
        {
            builder.AppendLine($"{diagnostic.Severity} {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        return builder.ToString();
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

        // Every BCL/runtime assembly available to this test host process, via the
        // trusted-platform-assemblies list .NET populates for every app — the standard,
        // package-free way to get a complete, correct reference set (including
        // System.ComponentModel.Primitives/System.ComponentModel.Annotations, needed for
        // [Description]/[Display] fixtures) without hand-maintaining a reference-assembly
        // list.
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies)
        {
            foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        // DataDictionary.Abstractions itself — fixture source uses its attributes
        // directly, so it must be an actual compiled reference, not source text.
        references.Add(MetadataReference.CreateFromFile(typeof(DataDictionary.Abstractions.DataDictionaryAttribute).Assembly.Location));

        return references;
    }
}
