using Microsoft.CodeAnalysis;

namespace Trivium.Analyzers.Tests.ObjectInitializerPlacement;

public class ObjectInitializerPlacementAnalyzerTests
{
    [Fact]
    public async Task InitializerOnSameLine_ReportsDiagnostic()
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

        var findings = await GetFindingsAsync(source);

        Assert.Single(findings);
    }

    [Fact]
    public async Task ImplicitCreationInitializerOnSameLine_ReportsDiagnostic()
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

        var findings = await GetFindingsAsync(source);

        Assert.Single(findings);
    }

    [Fact]
    public async Task InitializerOnOwnLine_IsClean()
    {
        const string source =
            """
            internal static class Sample
            {
                public static Widget Build()
                {
                    return new Widget
                    {
                        Value = 1
                    };
                }
            }

            internal sealed class Widget
            {
                public int Value { get; init; }
            }
            """;

        var findings = await GetFindingsAsync(source);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task InitializerWithoutObjectCreation_IsClean()
    {
        const string source =
            """
            internal static class Sample
            {
                public static Widget Build()
                {
                    return new Widget();
                }
            }

            internal sealed class Widget
            {
                public int Value { get; init; }
            }
            """;

        var findings = await GetFindingsAsync(source);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task AnonymousObjectOnSameLine_ReportsDiagnostic()
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

        var findings = await GetFindingsAsync(source);

        Assert.Single(findings);
    }

    [Fact]
    public async Task AnonymousObjectOnOwnLine_IsClean()
    {
        const string source =
            """
            internal static class Sample
            {
                public static object Build(int value)
                {
                    return new
                    {
                        Value = value
                    };
                }
            }
            """;

        var findings = await GetFindingsAsync(source);

        Assert.Empty(findings);
    }

    private static async Task<Diagnostic[]> GetFindingsAsync(string source)
    {
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new ObjectInitializerPlacementAnalyzer());
        return [.. diagnostics.Where(diagnostic => diagnostic.Id == ObjectInitializerPlacementAnalyzer.DiagnosticId)];
    }
}
