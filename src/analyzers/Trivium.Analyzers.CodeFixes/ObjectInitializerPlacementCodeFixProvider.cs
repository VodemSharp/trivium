using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Trivium.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ObjectInitializerPlacementCodeFixProvider))]
[Shared]
public sealed class ObjectInitializerPlacementCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [ObjectInitializerPlacementAnalyzer.DiagnosticId];

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

        if (token.Parent is not { } parent)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Put the object initializer on its own line",
                ct => ReformatAsync(context.Document, parent, ct),
                equivalenceKey: ObjectInitializerPlacementAnalyzer.DiagnosticId),
            diagnostic);
    }

    private static async Task<Document> ReformatAsync(Document document, SyntaxNode target, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null)
        {
            return document;
        }

        SyntaxNode? replacement = target switch
        {
            InitializerExpressionSyntax initializer => ReformatInitializer(initializer),
            AnonymousObjectCreationExpressionSyntax anonymous => ReformatAnonymousObject(anonymous),
            _ => null
        };

        if (replacement is null)
        {
            return document;
        }

        var newDocument = document.WithSyntaxRoot(root.ReplaceNode(target, replacement));

        return await Formatter.FormatAsync(newDocument, cancellationToken: ct);
    }

    private static InitializerExpressionSyntax ReformatInitializer(InitializerExpressionSyntax initializer)
    {
        var members = initializer.Expressions
            .Select(expression => expression.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))
            .ToArray();

        var separators = Enumerable
            .Range(1, Math.Max(0, members.Length - 1))
            .Select(_ => SyntaxFactory.Token(SyntaxKind.CommaToken));

        var newOpenBrace = initializer.OpenBraceToken.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        var newCloseBrace = initializer.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        return initializer
            .WithOpenBraceToken(newOpenBrace)
            .WithExpressions(SyntaxFactory.SeparatedList(members, separators))
            .WithCloseBraceToken(newCloseBrace);
    }

    private static AnonymousObjectCreationExpressionSyntax ReformatAnonymousObject(
        AnonymousObjectCreationExpressionSyntax anonymous)
    {
        var members = anonymous.Initializers
            .Select(member => member.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))
            .ToArray();

        var separators = Enumerable
            .Range(1, Math.Max(0, members.Length - 1))
            .Select(_ => SyntaxFactory.Token(SyntaxKind.CommaToken));

        var newOpenBrace = anonymous.OpenBraceToken.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        var newCloseBrace = anonymous.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        return anonymous
            .WithOpenBraceToken(newOpenBrace)
            .WithInitializers(SyntaxFactory.SeparatedList(members, separators))
            .WithCloseBraceToken(newCloseBrace);
    }
}
