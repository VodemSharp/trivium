using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trivium.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AwaitInConditionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TRIVSTYLE0005";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Hoist awaited calls out of conditions",
        "Assign the awaited call to a local instead of inlining it in a condition",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An awaited call in an if, while, do or for condition is assigned to a local first, so the branch or loop decides over a value.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.IfStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.ForStatement);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var condition = Condition(context.Node);

        if (condition is null)
        {
            return;
        }

        foreach (var awaited in condition
                     .DescendantNodesAndSelf(IsSameScope)
                     .OfType<AwaitExpressionSyntax>())
        {
            context.ReportDiagnostic(Diagnostic.Create(_rule, awaited.GetLocation()));
        }
    }

    private static ExpressionSyntax? Condition(SyntaxNode statement)
    {
        return statement switch
        {
            IfStatementSyntax ifStatement => ifStatement.Condition,
            WhileStatementSyntax whileStatement => whileStatement.Condition,
            DoStatementSyntax doStatement => doStatement.Condition,
            ForStatementSyntax forStatement => forStatement.Condition,
            _ => null
        };
    }

    // A nested function starts a fresh scope, so an await inside a lambda or local function declared in the
    // condition is judged by its own body, not by the condition it sits in.
    private static bool IsSameScope(SyntaxNode node)
    {
        return node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax;
    }
}
