using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Trivium.Analyzers.CodeFixes;

namespace Trivium.Analyzers.Tests.CancellationTokenNaming;

public class CancellationTokenNamingCodeFixTests
{
    [Fact]
    public async Task CodeFix_WhenParameterNotNamedCt_RenamesToCt()
    {
        const string source =
            """
            using System.Threading;

            internal static class Sample
            {
                public static void Run(CancellationToken token) { }
            }
            """;

        var renamed = await ApplyFixAsync(source);

        Assert.Contains("CancellationToken ct)", renamed);
    }

    [Fact]
    public async Task CodeFix_WhenLocalNotNamedCt_RenamesToCt()
    {
        const string source =
            """
            using System.Threading;

            internal static class Sample
            {
                public static void Run()
                {
                    CancellationToken token = default;
                    _ = token;
                }
            }
            """;

        var renamed = await ApplyFixAsync(source);

        Assert.Contains("CancellationToken ct = default;", renamed);
    }

    [Fact]
    public async Task FixAll_WhenTwoMethodsHaveTokens_RenamesBoth()
    {
        const string source =
            """
            using System.Threading;

            internal static class Sample
            {
                public static void First(CancellationToken token) { }

                public static void Second(CancellationToken other) { }
            }
            """;

        var renamed = await ApplyFixAllAsync(source);

        Assert.Contains("First(CancellationToken ct)", renamed);
        Assert.Contains("Second(CancellationToken ct)", renamed);
    }

    private static async Task<string> ApplyFixAllAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace
            .AddProject("CodeFixTests", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(AnalyzerHarness.References);

        var document = project.AddDocument("Sample.cs", SourceText.From(source));
        var compilation = await document.Project.GetCompilationAsync();
        var diagnostics = await compilation!
            .WithAnalyzers([new CancellationTokenNamingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();

        var provider = new CancellationTokenNamingCodeFixProvider();
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            codeActionEquivalenceKey: CancellationTokenNamingAnalyzer.DiagnosticId,
            diagnosticIds: provider.FixableDiagnosticIds,
            fixAllDiagnosticProvider: new AnalyzerDiagnosticProvider(diagnostics),
            cancellationToken: CancellationToken.None);

        var action = await provider.GetFixAllProvider().GetFixAsync(context);
        var operations = await action!.GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changedDocument = changedSolution.GetDocument(document.Id)!;

        var changedText = await changedDocument.GetTextAsync();
        return changedText.ToString();
    }

    private sealed class AnalyzerDiagnosticProvider(ImmutableArray<Diagnostic> diagnostics)
        : FixAllContext.DiagnosticProvider
    {
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>([]);
        }

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
        }
    }

    private static async Task<string> ApplyFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace
            .AddProject("CodeFixTests", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(AnalyzerHarness.References);

        var document = project.AddDocument("Sample.cs", SourceText.From(source));
        var compilation = await document.Project.GetCompilationAsync();
        var diagnostics = await compilation!
            .WithAnalyzers([new CancellationTokenNamingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.Single(d => d.Id == CancellationTokenNamingAnalyzer.DiagnosticId);

        var actions = new List<CodeAction>();
        var codeFixContext = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        var provider = new CancellationTokenNamingCodeFixProvider();
        await provider.RegisterCodeFixesAsync(codeFixContext);

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyChanges = operations.OfType<ApplyChangesOperation>().Single();
        var changedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;

        var changedText = await changedDocument.GetTextAsync();
        return changedText.ToString();
    }
}
