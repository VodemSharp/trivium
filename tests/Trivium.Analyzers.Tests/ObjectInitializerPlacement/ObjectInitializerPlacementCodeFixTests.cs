using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Trivium.Analyzers.CodeFixes;

namespace Trivium.Analyzers.Tests.ObjectInitializerPlacement;

public class ObjectInitializerPlacementCodeFixTests
{
    [Fact]
    public async Task CodeFix_WhenInitializerOnSameLine_MovesToOwnLine()
    {
        const string source =
            """
            internal static class Sample
            {
                public static Widget Build()
                {
                    return new Widget { Value = 1 };
                }
            }

            internal sealed class Widget
            {
                public int Value { get; init; }
            }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.Matches(@"new Widget\s*\n\s*\{", fixedSource);
    }

    [Fact]
    public async Task CodeFix_WhenImplicitInitializerOnSameLine_MovesToOwnLine()
    {
        const string source =
            """
            internal static class Sample
            {
                public static Widget Build()
                {
                    Widget widget = new() { Value = 1 };
                    return widget;
                }
            }

            internal sealed class Widget
            {
                public int Value { get; init; }
            }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.Matches(@"new\(\)\s*\n\s*\{", fixedSource);
    }

    [Fact]
    public async Task CodeFix_WhenAnonymousObjectOnSameLine_MovesToOwnLine()
    {
        const string source =
            """
            internal static class Sample
            {
                public static object Build(int value)
                {
                    return new { Value = value };
                }
            }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.Matches(@"new\s*\n\s*\{", fixedSource);
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
            .WithAnalyzers([new ObjectInitializerPlacementAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.Single(d => d.Id == ObjectInitializerPlacementAnalyzer.DiagnosticId);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        var provider = new ObjectInitializerPlacementCodeFixProvider();
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
}
