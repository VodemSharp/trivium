using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trivium.Analyzers.Tests;

internal static class AnalyzerHarness
{
    internal static ImmutableArray<MetadataReference> References { get; } = BuildReferences();

    public static Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        return GetDiagnosticsAsync(source, new CancellationTokenNamingAnalyzer());
    }

    public static Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        params DiagnosticAnalyzer[] analyzers)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [syntaxTree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation
            .WithAnalyzers([.. analyzers])
            .GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        return
        [
            .. AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
        ];
    }
}
