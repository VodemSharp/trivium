using Microsoft.CodeAnalysis;

namespace Trivium.Analyzers.Tests.HoistedAwait;

public class HoistedAwaitAnalyzerTests
{
    private static async Task<Diagnostic[]> GetFindingsAsync(string body)
    {
        var source =
            $$"""
            using System;
            using System.Threading.Tasks;

            internal static class Sample
            {
                public static async Task Run()
                {
            {{body}}
                }

                private static Task<int> GetAsync() => Task.FromResult(1);

                private static void Consume(int value) { }
            }
            """;

        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new HoistedAwaitAnalyzer());
        return [.. diagnostics.Where(diagnostic => diagnostic.Id == HoistedAwaitAnalyzer.DiagnosticId)];
    }

    [Fact]
    public async Task Argument_WhenAwaitedCallInlined_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync("Consume(await GetAsync());");

        Assert.Single(findings);
    }

    [Fact]
    public async Task Argument_WhenAwaitedResultNestedInExpression_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync("Consume((await GetAsync()) + 1);");

        Assert.Single(findings);
    }

    [Fact]
    public async Task Parenthesized_WhenAwaitedResultChained_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync("var text = (await GetAsync()).ToString();");

        Assert.Single(findings);
    }

    [Fact]
    public async Task Assignment_WhenAwaitedCallAssigned_IsClean()
    {
        var findings = await GetFindingsAsync(
            """
            var value = await GetAsync();
            Consume(value);
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Assignment_WhenAwaitedResultSuppressed_IsClean()
    {
        var findings = await GetFindingsAsync(
            """
            var value = (await GetAsync())!;
            Consume(value);
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Statement_WhenAwaitedCallStandalone_IsClean()
    {
        var findings = await GetFindingsAsync("await Task.Delay(1);");

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Return_WhenAwaitedCallReturned_IsClean()
    {
        const string source =
            """
            using System.Threading.Tasks;

            internal static class Sample
            {
                public static async Task<int> Run()
                {
                    return await GetAsync();
                }

                private static Task<int> GetAsync() => Task.FromResult(1);
            }
            """;

        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new HoistedAwaitAnalyzer());

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Id == HoistedAwaitAnalyzer.DiagnosticId));
    }

    [Fact]
    public async Task Lambda_WhenAwaitedCallInsideLambda_IsClean()
    {
        var findings = await GetFindingsAsync(
            """
            Func<Task<int>> action = async () => await GetAsync();
            _ = action;
            """);

        Assert.Empty(findings);
    }
}
