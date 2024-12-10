using System.Collections.Immutable;
using CeeSharp.Core.Syntax;
using CeeSharp.Core.Syntax.Nodes;
using CeeSharp.Core.Syntax.Nodes.Declarations;

namespace CeeSharp.Core.Parsing;

public sealed class Parser(Diagnostics diagnostics, TokenStream tokenStream)
{
    private bool isInErrorRecovery;

    private SyntaxToken Current => tokenStream.Current;
    private SyntaxToken Previous => tokenStream.Previous;

    public CompilationUnitNode Parse()
    {
        var usings = ParseUsings();
        var declarations = ParseNamespaceOrTypeDeclarations(DeclarationContext.Namespace);

        if (!TryExpect(TokenKind.EndOfFile, out _)) SkipUntil(); // Skip until the end

        isInErrorRecovery = false;

        var endOfFile = Current;

        return new CompilationUnitNode(usings, declarations, endOfFile);
    }

    private SyntaxToken ExpectIdentifier()
    {
        if (!TryExpect(TokenKind.Identifier, out var token))
            diagnostics.ReportError(token.EndTextPosition, "Identifier expected");

        return token;
    }

    private SyntaxToken Expect(TokenKind kind, string text)
    {
        if (!TryExpect(kind, out var token))
            diagnostics.ReportError(Previous.EndPosition, $"{text} expected");

        return token;
    }

    private bool TryExpect(TokenKind kind, out SyntaxToken token)
    {
        if (Current.Kind == kind)
        {
            token = Current;

            tokenStream.Advance();

            return true;
        }

        if (!isInErrorRecovery) isInErrorRecovery = true;

        token = new SyntaxToken(kind, "", Previous.EndPosition);

        return false;
    }

    private void SkipUntil(params TokenKind[] synchronizingTokens)
    {
        while (Current.Kind != TokenKind.EndOfFile &&
               !synchronizingTokens.Contains(Current.Kind)) tokenStream.Advance();

        isInErrorRecovery = false;
    }

    private ImmutableArray<UsingDirectiveNode> ParseUsings()
    {
        var usings = ImmutableArray.CreateBuilder<UsingDirectiveNode>();

        while (Current.Kind == TokenKind.Using)
        {
            var directive = ParseUsing();
            usings.Add(directive);

            if (isInErrorRecovery)
                SkipUntil(TokenKind.Using, TokenKind.Class);
        }

        return usings.ToImmutable();
    }

    private UsingDirectiveNode ParseUsing()
    {
        var usingKeyword = Expect(TokenKind.Using, "using");
        var identifier = ExpectIdentifier();
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new UsingDirectiveNode(usingKeyword, identifier, semicolon);
    }

    private ImmutableArray<DeclarationNode> ParseNamespaceOrTypeDeclarations(DeclarationContext declarationContext)
    {
        var declarations = ImmutableArray.CreateBuilder<DeclarationNode>();

        while (Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            var declaration = ParseNamespaceOrTypeDeclaration(declarationContext);
            if (declaration != null) declarations.Add(declaration);

            if (isInErrorRecovery) SkipUntil(TokenKind.Namespace, TokenKind.Class);
        }

        return declarations.ToImmutable();
    }

    private DeclarationNode? ParseNamespaceOrTypeDeclaration(DeclarationContext declarationContext)
    {
        switch (Current.Kind)
        {
            case TokenKind.Namespace:
                return ParseNamespaceDeclaration();
            case TokenKind.Class:
                return ParseTypeDeclaration(declarationContext);
            default:
                if (!isInErrorRecovery)
                {
                    diagnostics.ReportError(Current.Position, "Type or namespace definition, or end-of-file expected");
                    isInErrorRecovery = true;
                }

                return null;
        }
    }

    private NamespaceDeclarationNode ParseNamespaceDeclaration()
    {
        var namespaceKeyword = Expect(TokenKind.Namespace, "namespace");
        var name = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var usings = ParseUsings();
        var declarations = ParseNamespaceOrTypeDeclarations(DeclarationContext.Namespace);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new NamespaceDeclarationNode(namespaceKeyword, name, openBrace, usings, declarations, closeBrace);
    }

    private ImmutableArray<DeclarationNode> ParseTypeDeclarations(DeclarationContext declarationContext)
    {
        var declarations = ImmutableArray.CreateBuilder<DeclarationNode>();

        while (Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            var declaration = ParseTypeDeclaration(DeclarationContext.Class);
            if (declaration != null) declarations.Add(declaration);

            if (isInErrorRecovery && DeclarationAcceptsToken(declarationContext, Current.Kind))
            {
                isInErrorRecovery = false;

                break;
            }

            if (isInErrorRecovery) SkipUntil(TokenKind.Class);
        }

        return declarations.ToImmutable();
    }

    private DeclarationNode? ParseTypeDeclaration(DeclarationContext declarationContext)
    {
        switch (Current.Kind)
        {
            case TokenKind.Class:
                return ParseClassDeclaration(declarationContext);
            default:
                if (!isInErrorRecovery) isInErrorRecovery = true;

                return null;
        }
    }

    private ClassDeclarationNode ParseClassDeclaration(DeclarationContext declarationContext)
    {
        var classKeyword = Expect(TokenKind.Class, "class");
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations(declarationContext);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new ClassDeclarationNode(classKeyword, identifier, openBrace, declarations, closeBrace);
    }

    private static bool DeclarationAcceptsToken(DeclarationContext declarationContext, TokenKind tokenKind)
    {
        switch (declarationContext)
        {
            case DeclarationContext.Namespace:
                return tokenKind is TokenKind.Namespace or TokenKind.Class;
            case DeclarationContext.Class:
                return tokenKind is TokenKind.Class;
            default:
                return false;
        }
    }

    private enum DeclarationContext
    {
        Namespace,
        Class
    }
}