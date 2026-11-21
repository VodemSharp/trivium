using Microsoft.CodeAnalysis;

namespace Trivium.Analyzers.Tests.MultilineStatementSpacing;

public class MultilineStatementSpacingAnalyzerTests
{
    [Fact]
    public async Task MultilineStatementFollowedByStatement_ReportsDiagnostic()
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

        var findings = await GetFindingsAsync(source);

        Assert.Single(findings);
    }

    [Fact]
    public async Task MultilineStatementFollowedByBlankLine_IsClean()
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

        var findings = await GetFindingsAsync(source);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task SingleLineStatements_AreClean()
    {
        const string source =
            """
            internal static class Sample
            {
                public static void Run()
                {
                    var a = 1;
                    var b = 2;
                    Consume(a + b);
                }

                private static void Consume(int value)
                {
                }
            }
            """;

        var findings = await GetFindingsAsync(source);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task MultilineBlockStatementFollowedByStatement_ReportsDiagnostic()
    {
        const string source =
            """
            internal static class Sample
            {
                public static void Run(bool flag)
                {
                    if (flag)
                    {
                        Consume(1);
                    }
                    Consume(2);
                }

                private static void Consume(int value)
                {
                }
            }
            """;

        var findings = await GetFindingsAsync(source);

        Assert.Single(findings);
    }

    [Fact]
    public async Task MultilineBlockStatementFollowedByBlankLine_IsClean()
    {
        const string source =
            """
            internal static class Sample
            {
                public static void Run(bool flag)
                {
                    if (flag)
                    {
                        Consume(1);
                    }

                    Consume(2);
                }

                private static void Consume(int value)
                {
                }
            }
            """;

        var findings = await GetFindingsAsync(source);

        Assert.Empty(findings);
    }

    private static async Task<Diagnostic[]> GetFindingsAsync(string source)
    {
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new MultilineStatementSpacingAnalyzer());
        return [.. diagnostics.Where(diagnostic => diagnostic.Id == MultilineStatementSpacingAnalyzer.DiagnosticId)];
    }
}
