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
        var declarations = ParseDeclarations();

        return new CompilationUnitNode(usings, declarations);
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
            diagnostics.ReportError(Previous.EndPosition, $"Expected {text}");

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
                SkipUntil(TokenKind.Using, TokenKind.Class, TokenKind.EndOfFile);
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

    private ImmutableArray<DeclarationNode> ParseDeclarations()
    {
        var declarations = ImmutableArray.CreateBuilder<DeclarationNode>();

        while (Current.Kind != TokenKind.EndOfFile)
        {
            var declaration = ParseDeclaration();
            if (declaration != null) declarations.Add(declaration);

            if (isInErrorRecovery) SkipUntil(TokenKind.Class, TokenKind.EndOfFile);
        }

        return declarations.ToImmutable();
    }

    private DeclarationNode? ParseDeclaration()
    {
        switch (Current.Kind)
        {
            case TokenKind.Class:
                return ParseClassDeclaration();
            default:
                if (!isInErrorRecovery)
                {
                    diagnostics.ReportError(Current.Position, "Expected declaration");
                    isInErrorRecovery = true;
                }

                return null;
        }
    }

    private ClassDeclarationNode ParseClassDeclaration()
    {
        var classKeyword = Expect(TokenKind.Class, "class");
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new ClassDeclarationNode(classKeyword, identifier, openBrace, closeBrace);
    }
}