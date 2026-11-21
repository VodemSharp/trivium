using Microsoft.CodeAnalysis;

namespace Trivium.Analyzers.Tests.AwaitInCondition;

public class AwaitInConditionAnalyzerTests
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

                private static Task<bool> CheckAsync() => Task.FromResult(true);
            }
            """;

        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new AwaitInConditionAnalyzer());
        return [.. diagnostics.Where(diagnostic => diagnostic.Id == AwaitInConditionAnalyzer.DiagnosticId)];
    }

    [Fact]
    public async Task If_WhenAwaitedCallInCondition_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            if (await CheckAsync())
            {
                await Task.Delay(1);
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task If_WhenNegatedAwaitedCallInCondition_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            if (!await CheckAsync())
            {
                await Task.Delay(1);
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task While_WhenAwaitedCallInCondition_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            while (await CheckAsync())
            {
                await Task.Delay(1);
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task Do_WhenAwaitedCallInCondition_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            do
            {
                await Task.Delay(1);
            }
            while (await CheckAsync());
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task For_WhenAwaitedCallInCondition_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            for (var i = 0; await CheckAsync(); i++)
            {
                await Task.Delay(1);
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task Condition_WhenAwaitedCallsNestedInAnArgument_ReportsEachDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            if (await CheckAsync() && await Task.Run(async () => await CheckAsync()))
            {
                await Task.Delay(1);
            }
            """);

        Assert.Equal(2, findings.Length);
    }

    [Fact]
    public async Task Assignment_WhenAwaitedCallHoistedAboveTheIf_IsClean()
    {
        var findings = await GetFindingsAsync(
            """
            var ready = await CheckAsync();
            if (ready)
            {
                await Task.Delay(1);
            }
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Body_WhenAwaitedCallInsideTheIfBlock_IsClean()
    {
        var findings = await GetFindingsAsync(
            """
            if (Environment.TickCount > 0)
            {
                var ready = await CheckAsync();
                await Task.Delay(ready ? 1 : 2);
            }
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Initializer_WhenAwaitedCallInTheForInitializer_IsClean()
    {
        var findings = await GetFindingsAsync(
            """
            for (var ready = await CheckAsync(); ready; ready = false)
            {
                await Task.Delay(1);
            }
            """);

        Assert.Empty(findings);
    }
}
