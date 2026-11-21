using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Trivium.Analyzers.CodeFixes;

namespace Trivium.Analyzers.Tests.MultilineStatementSpacing;

public class MultilineStatementSpacingCodeFixTests
{
    [Fact]
    public async Task CodeFix_WhenMultilineStatementFollowedByStatement_InsertsBlankLine()
    {
        const string source =
            """
            internal static class Sample
            {
                public static void Run()
                {
                    var value = Combine(
                        1,
                        2);
                    Consume(value);
                }

                private static int Combine(int a, int b)
                {
                    return a + b;
                }

                private static void Consume(int value)
                {
                }
            }
            """;

        var fixedSource = await ApplyFixAsync(source);

        var normalized = fixedSource.Replace("\r\n", "\n");
        Assert.Matches(@"2\);\n\n\s*Consume\(", normalized);
    }

    [Fact]
    public async Task CodeFix_WhenMultilineStatementInsideIfBlock_InsertsBlankLine()
    {
        const string source =
            """
            internal static class Sample
            {
                public static void Run(bool condition)
                {
                    if (condition)
                    {
                        var value = Combine(
                            1,
                            2);
                        Consume(value);
                    }
                }

                private static int Combine(int a, int b)
                {
                    return a + b;
                }

                private static void Consume(int value)
                {
                }
            }
            """;

        var fixedSource = await ApplyFixAsync(source);

        var normalized = fixedSource.Replace("\r\n", "\n");
        Assert.Matches(@"2\);\n\n\s*Consume\(", normalized);
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
            .WithAnalyzers([new MultilineStatementSpacingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.Single(d => d.Id == MultilineStatementSpacingAnalyzer.DiagnosticId);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        var provider = new MultilineStatementSpacingCodeFixProvider();
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
