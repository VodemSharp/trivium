using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Trivium.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AwaitInConditionCodeFixProvider))]
[Shared]
public sealed class AwaitInConditionCodeFixProvider : CodeFixProvider
{
    private const string PreferredName = "awaited";

    public override ImmutableArray<string> FixableDiagnosticIds => [AwaitInConditionAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node.AncestorsAndSelf().OfType<AwaitExpressionSyntax>().FirstOrDefault() is not { } awaited
            || awaited.AncestorsAndSelf().OfType<IfStatementSyntax>().FirstOrDefault() is not { } statement)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Hoist the awaited call to a local",
                ct => HoistAsync(context.Document, awaited, statement, ct),
                equivalenceKey: AwaitInConditionAnalyzer.DiagnosticId),
            diagnostic);
    }

    private static async Task<Document> HoistAsync(
        Document document,
        AwaitExpressionSyntax awaited,
        IfStatementSyntax statement,
        CancellationToken ct)
    {
        var editor = await DocumentEditor.CreateAsync(document, ct);
        var name = CreateLocalName(statement);
        var type = editor.Generator.IdentifierName("var");
        var declaration = editor.Generator.LocalDeclarationStatement(type, name, awaited.WithoutTrivia());
        editor.InsertBefore(statement, declaration);
        editor.ReplaceNode(awaited, editor.Generator.IdentifierName(name));

        return await Formatter.FormatAsync(editor.GetChangedDocument(), cancellationToken: ct);
    }

    private static string CreateLocalName(SyntaxNode statement)
    {
        var block = statement.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
        var used = new HashSet<string>();

        if (block is not null)
        {
            used.UnionWith(block
                .DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
                .Select(token => token.ValueText));
        }

        var name = PreferredName;
        var suffix = 1;
        while (used.Contains(name))
        {
            name = PreferredName + suffix++;
        }

        return name;
    }
}
