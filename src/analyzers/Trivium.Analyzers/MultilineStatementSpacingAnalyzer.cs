using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trivium.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MultilineStatementSpacingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TRIVSTYLE0004";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Add a blank line after a multi-line statement",
        "Add a blank line after this multi-line statement",
        "Formatting",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A statement that spans multiple lines must be separated from the next statement by a blank line.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
        context.RegisterSyntaxNodeAction(AnalyzeCompilationUnit, SyntaxKind.CompilationUnit);
    }

    private static void AnalyzeCompilationUnit(SyntaxNodeAnalysisContext context)
    {
        var unit = (CompilationUnitSyntax)context.Node;
        var statements = unit.Members
            .OfType<GlobalStatementSyntax>()
            .Select(globalStatement => globalStatement.Statement)
            .ToList();

        AnalyzeStatements(context, statements);
    }

    private static void AnalyzeBlock(SyntaxNodeAnalysisContext context)
    {
        var block = (BlockSyntax)context.Node;
        AnalyzeStatements(context, block.Statements);
    }

    private static void AnalyzeStatements(SyntaxNodeAnalysisContext context, IReadOnlyList<StatementSyntax> statements)
    {
        for (var i = 0; i < statements.Count - 1; i++)
        {
            var current = statements[i];
            var next = statements[i + 1];

            var currentSpan = current.GetLocation().GetLineSpan();
            if (currentSpan.StartLinePosition.Line == currentSpan.EndLinePosition.Line)
            {
                continue;
            }

            var endLine = currentSpan.EndLinePosition.Line;
            var nextLine = next.GetLocation().GetLineSpan().StartLinePosition.Line;

            if (HasBlankLine(context.Node.SyntaxTree, endLine, nextLine))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(_rule, next.GetLocation()));
        }
    }

    private static bool HasBlankLine(SyntaxTree tree, int previousEndLine, int nextStartLine)
    {
        var lines = tree.GetText().Lines;

        for (var line = previousEndLine + 1; line < nextStartLine; line++)
        {
            if (string.IsNullOrWhiteSpace(lines[line].ToString()))
            {
                return true;
            }
        }

        return false;
    }
}
