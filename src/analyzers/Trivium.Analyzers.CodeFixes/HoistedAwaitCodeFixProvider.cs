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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HoistedAwaitCodeFixProvider))]
[Shared]
public sealed class HoistedAwaitCodeFixProvider : CodeFixProvider
{
    private const string PreferredName = "awaited";

    public override ImmutableArray<string> FixableDiagnosticIds => [HoistedAwaitAnalyzer.DiagnosticId];

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
            || awaited.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault() is not { } statement)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Hoist the awaited call to a local",
                ct => HoistAsync(context.Document, awaited, statement, ct),
                equivalenceKey: HoistedAwaitAnalyzer.DiagnosticId),
            diagnostic);
    }

    private static async Task<Document> HoistAsync(
        Document document,
        AwaitExpressionSyntax awaited,
        StatementSyntax statement,
        CancellationToken ct)
    {
        var editor = await DocumentEditor.CreateAsync(document, ct);
        var name = CreateLocalName(statement);
        var type = editor.Generator.IdentifierName("var");
        var declaration = editor.Generator.LocalDeclarationStatement(type, name, awaited.WithoutTrivia());
        editor.InsertBefore(statement, declaration);

        SyntaxNode target = awaited.Parent is ParenthesizedExpressionSyntax paren && IsChained(paren)
            ? paren
            : awaited;
        editor.ReplaceNode(target, editor.Generator.IdentifierName(name));

        return await Formatter.FormatAsync(editor.GetChangedDocument(), cancellationToken: ct);
    }

    private static bool IsChained(ParenthesizedExpressionSyntax paren)
    {
        return paren.Parent is MemberAccessExpressionSyntax
            or ElementAccessExpressionSyntax
            or InvocationExpressionSyntax;
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
