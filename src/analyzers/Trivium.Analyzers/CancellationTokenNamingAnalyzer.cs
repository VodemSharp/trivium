using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trivium.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CancellationTokenNamingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TRIVSTYLE0001";
    public const string PreferredName = "ct";

    private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";
    private const int ExemptionThreshold = 2;

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Name CancellationToken 'ct'",
        "Rename '{0}' to 'ct'",
        "Naming",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "CancellationToken parameters and locals are named 'ct' unless the scope declares two or more.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var cancellationTokenSymbol = context.Compilation.GetTypeByMetadataName(CancellationTokenMetadataName);
        if (cancellationTokenSymbol is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeParameter(nodeContext, cancellationTokenSymbol),
            SyntaxKind.Parameter);

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeLocal(nodeContext, cancellationTokenSymbol),
            SyntaxKind.VariableDeclarator);
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context, INamedTypeSymbol cancellationTokenSymbol)
    {
        var parameter = (ParameterSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not { } symbol
            || !IsCancellationToken(symbol.Type, cancellationTokenSymbol)
            || IsExempt(parameter, context, cancellationTokenSymbol, symbol.Name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_rule, parameter.Identifier.GetLocation(), symbol.Name));
    }

    private static void AnalyzeLocal(SyntaxNodeAnalysisContext context, INamedTypeSymbol cancellationTokenSymbol)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (declarator.Parent?.Parent is not LocalDeclarationStatementSyntax
            || context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not ILocalSymbol symbol
            || !IsCancellationToken(symbol.Type, cancellationTokenSymbol)
            || IsExempt(declarator, context, cancellationTokenSymbol, symbol.Name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_rule, declarator.Identifier.GetLocation(), symbol.Name));
    }

    private static bool IsExempt(
        SyntaxNode declaration,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol cancellationTokenSymbol,
        string name)
    {
        if (IsAllowedName(name))
        {
            return true;
        }

        var scope = FindEnclosingScope(declaration);
        return scope is not null
               && CountCancellationTokens(scope, context, cancellationTokenSymbol) >= ExemptionThreshold;
    }

    // The scope is the nearest enclosing member that is not itself a nested function: lambdas and local
    // functions are transparent, so a token declared inside one sees the tokens of its enclosing member.
    // This keeps the exemption satisfiable — if a member declares two or more tokens they may all use
    // descriptive names, avoiding the shadowing that naming two nested tokens 'ct' would cause.
    private static SyntaxNode? FindEnclosingScope(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    continue;
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                case PropertyDeclarationSyntax:
                case IndexerDeclarationSyntax:
                case TypeDeclarationSyntax:
                case CompilationUnitSyntax:
                    return ancestor;
            }
        }

        return null;
    }

    private static int CountCancellationTokens(
        SyntaxNode scope,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol cancellationTokenSymbol)
    {
        var count = 0;
        foreach (var node in scope.DescendantNodes())
        {
            var type = node switch
            {
                ParameterSyntax parameter =>
                    context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken)?.Type,
                VariableDeclaratorSyntax { Parent.Parent: LocalDeclarationStatementSyntax } declarator =>
                    context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is ILocalSymbol local
                        ? local.Type
                        : null,
                _ => null
            };

            if (IsCancellationToken(type, cancellationTokenSymbol))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsAllowedName(string name)
    {
        return name == PreferredName || name.All(character => character == '_');
    }

    private static bool IsCancellationToken(ITypeSymbol? type, INamedTypeSymbol cancellationTokenSymbol)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        return SymbolEqualityComparer.Default.Equals(type, cancellationTokenSymbol);
    }
}
