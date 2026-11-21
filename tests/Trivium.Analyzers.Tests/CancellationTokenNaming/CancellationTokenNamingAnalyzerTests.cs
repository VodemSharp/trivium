using Microsoft.CodeAnalysis;

namespace Trivium.Analyzers.Tests.CancellationTokenNaming;

public class CancellationTokenNamingAnalyzerTests
{
    private static async Task<Diagnostic[]> GetFindingsAsync(string source)
    {
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source);
        return [.. diagnostics.Where(diagnostic => diagnostic.Id == CancellationTokenNamingAnalyzer.DiagnosticId)];
    }

    [Fact]
    public async Task Parameter_WhenNotNamedCt_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal static class Sample
            {
                public static void Run(CancellationToken token) { }
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task Parameter_WhenNamedCt_IsClean()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal static class Sample
            {
                public static void Run(CancellationToken ct) { }
            }
            """);

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("_")]
    [InlineData("__")]
    [InlineData("___")]
    public async Task Parameter_WhenNamedWithUnderscores_IsClean(string name)
    {
        var findings = await GetFindingsAsync(
            $$"""
            using System.Threading;

            internal static class Sample
            {
                public static void Run(CancellationToken {{name}}) { }
            }
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Parameters_WhenTwoTokensInScope_AreExempt()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal static class Sample
            {
                public static void Run(CancellationToken first, CancellationToken second) { }
            }
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Field_WhenNotNamedCt_IsIgnored()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal static class Sample
            {
                private static readonly CancellationToken Token = default;
            }
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Local_WhenNotNamedCt_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
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
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task Local_WhenTwoTokensInScope_AreExempt()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal static class Sample
            {
                public static void Run()
                {
                    CancellationToken first = default;
                    CancellationToken second = default;
                    _ = first;
                    _ = second;
                }
            }
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NullableParameter_WhenNotNamedCt_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal static class Sample
            {
                public static void Run(CancellationToken? token) { }
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task LambdaParameter_WhenNotNamedCt_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            using System;
            using System.Threading;

            internal static class Sample
            {
                public static void Run()
                {
                    Action<CancellationToken> action = token => { };
                    action(default);
                }
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task RecordParameter_WhenNotNamedCt_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal record Sample(CancellationToken token);
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task PrimaryConstructorParameter_WhenNotNamedCt_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal sealed class Sample(CancellationToken token);
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task AnonymousMethodParameter_WhenNotNamedCt_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            using System;
            using System.Threading;

            internal static class Sample
            {
                public static void Run()
                {
                    Action<CancellationToken> action = delegate (CancellationToken token) { };
                    action(default);
                }
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task ParenthesizedLambda_WhenTwoTokensInScope_AreExempt()
    {
        var findings = await GetFindingsAsync(
            """
            using System;
            using System.Threading;

            internal static class Sample
            {
                public static void Run()
                {
                    Action<CancellationToken, CancellationToken> action = (first, second) => { };
                    action(default, default);
                }
            }
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Parameter_WhenMemberAlsoDeclaresLocalToken_IsExempt()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal static class Sample
            {
                public static void Run(CancellationToken ct)
                {
                    CancellationToken token = default;
                    _ = token;
                }
            }
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NestedLambda_WhenOuterMemberDeclaresToken_IsExempt()
    {
        var findings = await GetFindingsAsync(
            """
            using System;
            using System.Threading;

            internal static class Sample
            {
                public static void Run(CancellationToken ct)
                {
                    Action<CancellationToken> action = inner => { };
                    action(default);
                }
            }
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NestedLambda_WhenOnlyTokenInMember_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            using System;
            using System.Threading;

            internal static class Sample
            {
                public static void Run()
                {
                    Action<CancellationToken> action = inner => { };
                    action(default);
                }
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task OperatorLocal_WhenNotNamedCt_ReportsDiagnostic()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal readonly struct Sample
            {
                public static Sample operator +(Sample left, Sample right)
                {
                    CancellationToken token = default;
                    _ = token;
                    return left;
                }
            }
            """);

        Assert.Single(findings);
    }

    [Fact]
    public async Task Local_WhenNamedWithUnderscore_IsClean()
    {
        var findings = await GetFindingsAsync(
            """
            using System.Threading;

            internal static class Sample
            {
                public static void Run()
                {
                    CancellationToken _ = default;
                }
            }
            """);

        Assert.Empty(findings);
    }
}
