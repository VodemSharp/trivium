using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trivium.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HoistedAwaitAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TRIVSTYLE0002";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Hoist awaited calls",
        "Assign the awaited call to a local instead of inlining it",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An awaited call is assigned to a local rather than passed as an argument or chained on '(await …)'.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AwaitExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var awaited = (AwaitExpressionSyntax)context.Node;
        if (IsHoisted(awaited))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_rule, awaited.GetLocation()));
    }

    // An awaited call is allowed only where its result is consumed directly: assigned, returned, or a whole
    // statement. It is flagged when it is passed (anywhere) as an argument, or when the parenthesized result is
    // chained — '(await x).Member'. A nested function starts a fresh scope, so an await inside a lambda or local
    // function is judged by its own body, not by the argument the function is passed as.
    private static bool IsHoisted(AwaitExpressionSyntax awaited)
    {
        for (SyntaxNode node = awaited; node.Parent is { } parent; node = parent)
        {
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }

            switch (parent)
            {
                case ArgumentSyntax:
                case ParenthesizedExpressionSyntax parenthesized when IsChained(parenthesized):
                    return false;
                case AssignmentExpressionSyntax:
                case EqualsValueClauseSyntax:
                case ReturnStatementSyntax:
                case ArrowExpressionClauseSyntax:
                case ExpressionStatementSyntax:
                    return true;
            }
        }

        return true;
    }

    private static bool IsChained(ParenthesizedExpressionSyntax paren)
    {
        return paren.Parent is MemberAccessExpressionSyntax
            or ElementAccessExpressionSyntax
            or InvocationExpressionSyntax;
    }
}
