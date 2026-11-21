using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Trivium.Analyzers.CodeFixes;

namespace Trivium.Analyzers.Tests.AwaitInCondition;

public class AwaitInConditionCodeFixTests
{
    private const string ClassSource =
        """
        using System.Threading.Tasks;

        internal static class Sample
        {
            public static async Task Run()
            {
        {{body}}
            }

            private static Task<bool> CheckAsync()
            {
                return Task.FromResult(true);
            }
        }
        """;

    [Fact]
    public async Task CodeFix_WhenAwaitedCallInIfCondition_HoistsItAboveTheIf()
    {
        const string body =
            """
                    if (await CheckAsync())
                    {
                        await Task.Delay(1);
                    }
            """;

        var fixedSource = await ApplyFixAsync(body);

        var normalized = fixedSource.Replace("\r\n", "\n");
        Assert.Matches(@"var awaited = await CheckAsync\(\);\n\s*if \(awaited\)", normalized);
    }

    [Fact]
    public async Task CodeFix_WhenNegatedAwaitedCallInIfCondition_KeepsTheNegation()
    {
        const string body =
            """
                    if (!await CheckAsync())
                    {
                        await Task.Delay(1);
                    }
            """;

        var fixedSource = await ApplyFixAsync(body);

        var normalized = fixedSource.Replace("\r\n", "\n");
        Assert.Matches(@"var awaited = await CheckAsync\(\);\n\s*if \(!awaited\)", normalized);
    }

    [Fact]
    public async Task CodeFix_WhenAwaitedCallInWhileCondition_OffersNoFix()
    {
        const string body =
            """
                    while (await CheckAsync())
                    {
                        await Task.Delay(1);
                    }
            """;

        var actions = await GetActionsAsync(body);

        Assert.Empty(actions);
    }

    private static async Task<string> ApplyFixAsync(string body)
    {
        using var workspace = new AdhocWorkspace();
        var document = AddDocument(workspace, body);
        var actions = await GetActionsAsync(document);

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changedDocument = operations
            .OfType<ApplyChangesOperation>()
            .Single()
            .ChangedSolution
            .GetDocument(document.Id)!;

        var text = await changedDocument.GetTextAsync();
        return text.ToString();
    }

    private static async Task<List<CodeAction>> GetActionsAsync(string body)
    {
        using var workspace = new AdhocWorkspace();
        var document = AddDocument(workspace, body);

        return await GetActionsAsync(document);
    }

    private static async Task<List<CodeAction>> GetActionsAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync();
        var diagnostics = await compilation!
            .WithAnalyzers([new AwaitInConditionAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.Single(item => item.Id == AwaitInConditionAnalyzer.DiagnosticId);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        var provider = new AwaitInConditionCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        return actions;
    }

    private static Document AddDocument(AdhocWorkspace workspace, string body)
    {
        var source = ClassSource.Replace("{{body}}", body);
        var project = workspace
            .AddProject("CodeFixTests", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(AnalyzerHarness.References);

        return project.AddDocument("Sample.cs", SourceText.From(source));
    }
}
