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
        var usings = ParseUsings(DeclarationKind.Namespace);
        var declarations = ParseNamespaceOrTypeDeclarations(DeclarationKind.Namespace);

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

            isInErrorRecovery = false;

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

    private ImmutableArray<UsingDirectiveNode> ParseUsings(DeclarationKind declarationContext)
    {
        var usings = ImmutableArray.CreateBuilder<UsingDirectiveNode>();

        while (Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            if (Current.Kind == TokenKind.Using)
            {
                var directive = ParseUsing();
                usings.Add(directive);
            }
            else
            {
                if (DeclarationAcceptsToken(declarationContext, Current.Kind))
                    break;

                diagnostics.ReportError(Current.Position,
                    "The compilation unit or namespace contains an invalid declaration or directive");

                isInErrorRecovery = true;
            }

            if (isInErrorRecovery)
                SkipUntil(TokenKind.Using, TokenKind.Namespace, TokenKind.Class);
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

    private ImmutableArray<DeclarationNode> ParseNamespaceOrTypeDeclarations(DeclarationKind declarationContext)
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

    private DeclarationNode? ParseNamespaceOrTypeDeclaration(DeclarationKind declarationContext)
    {
        var modifiers = ParseModifiers();

        switch (Current.Kind)
        {
            case TokenKind.Namespace when modifiers.IsEmpty:
                return ParseNamespaceDeclaration();
            case TokenKind.Class:
                return ParseTypeDeclaration(declarationContext, modifiers);
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
        var usings = ParseUsings(DeclarationKind.Namespace);
        var declarations = ParseNamespaceOrTypeDeclarations(DeclarationKind.Namespace);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new NamespaceDeclarationNode(namespaceKeyword, name, openBrace, usings, declarations, closeBrace);
    }

    private ImmutableArray<SyntaxToken> ParseModifiers()
    {
        var modifiers = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind.IsModifier())
        {
            modifiers.Add(Current);
            tokenStream.Advance();
        }

        return modifiers.ToImmutable();
    }

    private void ValidateModifiers<TNode>(DeclarationKind declarationContext, ImmutableArray<SyntaxToken> modifiers)
        where TNode : IMemberNode
    {
        var seenModifiers = new HashSet<TokenKind>();

        foreach (var modifier in modifiers)
            if (!seenModifiers.Add(modifier.Kind))
                diagnostics.ReportError(modifier.Position, $"Duplicate '{modifier.Text}' modifier");

        seenModifiers.Clear();

        foreach (var modifier in modifiers)
        {
            if (!seenModifiers.Add(modifier.Kind))
                continue;

            if (!TNode.IsModifierValid(declarationContext, modifier.Kind))
                diagnostics.ReportError(modifier.Position,
                    $"The modifier '{modifier.Text}' is not valid for this item");
        }
    }

    private ImmutableArray<DeclarationNode> ParseTypeDeclarations(DeclarationKind declarationContext)
    {
        var declarations = ImmutableArray.CreateBuilder<DeclarationNode>();

        while (!isInErrorRecovery && Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            var modifiers = ParseModifiers();
            var declaration = ParseTypeDeclaration(declarationContext, modifiers);
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

    private DeclarationNode? ParseTypeDeclaration(DeclarationKind declarationContext,
        ImmutableArray<SyntaxToken> modifiers)
    {
        switch (Current.Kind)
        {
            case TokenKind.Class:
                return ParseClassDeclaration(declarationContext, modifiers);
            default:
                if (!isInErrorRecovery) isInErrorRecovery = true;

                return null;
        }
    }

    private ClassDeclarationNode ParseClassDeclaration(DeclarationKind declarationContext,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ClassDeclarationNode>(declarationContext, modifiers);

        var classKeyword = Expect(TokenKind.Class, "class");
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations(DeclarationKind.Class);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new ClassDeclarationNode(modifiers, classKeyword, identifier, openBrace, declarations, closeBrace);
    }

    private static bool DeclarationAcceptsToken(DeclarationKind declarationKind, TokenKind tokenKind)
    {
        switch (declarationKind)
        {
            case DeclarationKind.Namespace:
                return tokenKind.IsModifier() || tokenKind is TokenKind.Using or TokenKind.Namespace or TokenKind.Class;
            case DeclarationKind.Class:
                return tokenKind.IsModifier() || tokenKind is TokenKind.Class;
            default:
                return false;
        }
    }
}