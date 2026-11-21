using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trivium.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ObjectInitializerPlacementAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TRIVSTYLE0003";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Put object initializer on its own line",
        "Put the object initializer on its own line",
        "Formatting",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "An object initializer's opening brace belongs on a new line, not on the line of the object creation " +
            "expression (anonymous objects included).");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression,
            SyntaxKind.AnonymousObjectCreationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        SyntaxToken? openBrace = context.Node switch
        {
            BaseObjectCreationExpressionSyntax { Initializer: { } initializer } => initializer.OpenBraceToken,
            AnonymousObjectCreationExpressionSyntax anonymous => anonymous.OpenBraceToken,
            _ => null
        };

        if (openBrace is not { } brace)
        {
            return;
        }

        var previousTokenLine = brace.GetPreviousToken().GetLocation().GetLineSpan().EndLinePosition.Line;
        var openBraceLine = brace.GetLocation().GetLineSpan().StartLinePosition.Line;

        if (previousTokenLine == openBraceLine)
        {
            context.ReportDiagnostic(Diagnostic.Create(_rule, brace.GetLocation()));
        }
    }
}
