using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Trivium.Analyzers.CodeFixes;

namespace Trivium.Analyzers.Tests.HoistedAwait;

public sealed partial class HoistedAwaitCodeFixTests
{
    [Fact]
    public async Task CodeFix_WhenAwaitedCallInArgument_HoistsToLocal()
    {
        const string source =
            """
            using System.Threading.Tasks;

            internal static class Sample
            {
                public static async Task Run()
                {
                    Consume(await GetAsync());
                }

                private static Task<int> GetAsync() => Task.FromResult(1);

                private static void Consume(int value) { }
            }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.Contains("var awaited = await GetAsync();", fixedSource);
        Assert.Contains("Consume(awaited);", fixedSource);
    }

    [Fact]
    public async Task CodeFix_WhenAwaitedResultChained_HoistsToLocal()
    {
        const string source =
            """
            using System.Threading.Tasks;

            internal static class Sample
            {
                public static async Task Run()
                {
                    var text = (await GetAsync()).ToString();
                    Consume(text.Length);
                }

                private static Task<int> GetAsync() => Task.FromResult(1);

                private static void Consume(int value) { }
            }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.Contains("var awaited = await GetAsync();", fixedSource);
        Assert.Contains("awaited.ToString()", fixedSource);
    }

    [Fact]
    public async Task FixAll_WhenTwoMethodsAwaitArguments_HoistsBoth()
    {
        const string source =
            """
            using System.Threading.Tasks;

            internal static class Sample
            {
                public static async Task First()
                {
                    Consume(await GetAsync());
                }

                public static async Task Second()
                {
                    Consume(await GetAsync());
                }

                private static Task<int> GetAsync() => Task.FromResult(1);

                private static void Consume(int value) { }
            }
            """;

        var fixedSource = await ApplyFixAllAsync(source);

        var hoists = HoistedAwaitRegex().Matches(fixedSource);
        Assert.Equal(2, hoists.Count);
        Assert.DoesNotContain("Consume(await GetAsync())", fixedSource);
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
            .WithAnalyzers([new HoistedAwaitAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.Single(d => d.Id == HoistedAwaitAnalyzer.DiagnosticId);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        var provider = new HoistedAwaitCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changedDocument = operations
            .OfType<ApplyChangesOperation>()
            .Single()
            .ChangedSolution
            .GetDocument(document.Id)!;

        var text = await changedDocument.GetTextAsync();
        return text.ToString();
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
            .WithAnalyzers([new HoistedAwaitAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();

        var provider = new HoistedAwaitCodeFixProvider();
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            codeActionEquivalenceKey: HoistedAwaitAnalyzer.DiagnosticId,
            diagnosticIds: provider.FixableDiagnosticIds,
            fixAllDiagnosticProvider: new AnalyzerDiagnosticProvider(diagnostics),
            cancellationToken: CancellationToken.None);

        var action = await provider.GetFixAllProvider().GetFixAsync(context);
        var operations = await action!.GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changedDocument = changedSolution.GetDocument(document.Id)!;

        var text = await changedDocument.GetTextAsync();
        return text.ToString();
    }

    [GeneratedRegex(@"var awaited = await GetAsync\(\);")]
    private static partial Regex HoistedAwaitRegex();

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
}
