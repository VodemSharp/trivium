using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Trivium.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MultilineStatementSpacingCodeFixProvider))]
[Shared]
public sealed class MultilineStatementSpacingCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [MultilineStatementSpacingAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        var statement = token.Parent?.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();

        if (statement is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add a blank line",
                ct => AddBlankLineAsync(context.Document, statement, ct),
                equivalenceKey: MultilineStatementSpacingAnalyzer.DiagnosticId),
            diagnostic);
    }

    private static async Task<Document> AddBlankLineAsync(
        Document document,
        StatementSyntax statement,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null)
        {
            return document;
        }

        var leadingTrivia = statement.GetLeadingTrivia();
        var newStatement = statement.WithLeadingTrivia(
            leadingTrivia.Insert(0, SyntaxFactory.LineFeed));

        var newDocument = document.WithSyntaxRoot(root.ReplaceNode(statement, newStatement));

        return await Formatter.FormatAsync(newDocument, cancellationToken: ct);
    }
}
