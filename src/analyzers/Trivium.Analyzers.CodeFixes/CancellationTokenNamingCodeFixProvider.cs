using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace Trivium.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CancellationTokenNamingCodeFixProvider))]
[Shared]
public sealed class CancellationTokenNamingCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [CancellationTokenNamingAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        if (root is null || semanticModel is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            var symbol = token.Parent switch
            {
                ParameterSyntax parameter =>
                    semanticModel.GetDeclaredSymbol(parameter, context.CancellationToken),
                VariableDeclaratorSyntax declarator =>
                    semanticModel.GetDeclaredSymbol(declarator, context.CancellationToken),
                _ => null
            };

            if (symbol is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Rename to '{CancellationTokenNamingAnalyzer.PreferredName}'",
                    ct => RenameAsync(context.Document, symbol, ct),
                    equivalenceKey: CancellationTokenNamingAnalyzer.DiagnosticId),
                diagnostic);
        }
    }

    private static async Task<Document> RenameAsync(Document document, ISymbol symbol, CancellationToken ct)
    {
        var solution = await Renamer.RenameSymbolAsync(
            document.Project.Solution,
            symbol,
            new SymbolRenameOptions(),
            CancellationTokenNamingAnalyzer.PreferredName,
            ct);

        return solution.GetDocument(document.Id)!;
    }
}
