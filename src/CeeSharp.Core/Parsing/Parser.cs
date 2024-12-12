using System.Collections.Immutable;
using CeeSharp.Core.Syntax;
using CeeSharp.Core.Syntax.Nodes;
using CeeSharp.Core.Syntax.Nodes.Declarations;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Parsing;

public sealed class Parser(Diagnostics diagnostics, TokenStream tokenStream)
{
    private readonly ImmutableArray<SyntaxTrivia>.Builder skippedTokens = ImmutableArray.CreateBuilder<SyntaxTrivia>();
    private bool isInErrorRecovery;

    private SyntaxToken Current => tokenStream.Current;
    
    private SyntaxToken Previous => tokenStream.Previous;

    public CompilationUnitNode Parse()
    {
        skippedTokens.Clear();
        isInErrorRecovery = false;

        var usings = ParseUsings(DeclarationKind.Namespace);
        var declarations = ParseNamespaceOrTypeDeclarations(DeclarationKind.Namespace);

        if (!TryExpect(TokenKind.EndOfFile, out var endOfFile))
            SkipUntilEnd();

        isInErrorRecovery = false;

        return new CompilationUnitNode(usings, declarations, endOfFile);
    }

    private SyntaxToken ExpectIdentifier()
    {
        if (!TryExpect(TokenKind.Identifier, out var token))
            diagnostics.ReportError(token.EndTextPosition, "Identifier expected");

        return token;
    }
    
    private SyntaxToken Expect(TokenKind kind)
    {
        _ = TryExpect(kind, out var token);
        
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
            var current = Current;

            token = Current with
            {
                LeadingTrivia = current.LeadingTrivia.AddRange(skippedTokens)
            };

            skippedTokens.Clear();

            tokenStream.Advance();

            isInErrorRecovery = false;

            return true;
        }

        if (!isInErrorRecovery) isInErrorRecovery = true;

        token = new SyntaxToken(kind, "", Previous.EndPosition);

        return false;
    }

    private void SkipUntilEnd()
    {
        while (Current.Kind != TokenKind.EndOfFile)
            tokenStream.Advance();

        isInErrorRecovery = false;
    }

    private void Synchronize(DeclarationKind context, params TokenKind[] synchronizingTokens)
    {
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (IsTokenValidForDeclaration(context, Current.Kind) || synchronizingTokens.Contains(Current.Kind))
            {
                isInErrorRecovery = false;
                return;
            }

            skippedTokens.Add(new TokenSyntaxTrivia(TriviaKind.SkippedToken, Current, Current.Position));

            tokenStream.Advance();
        }
    }

    private TypeSyntax ParseType()
    {
        var type = ParseNonArrayType();

        while (Current.Kind == TokenKind.OpenBracket)
        {
            var openBracket = Expect(TokenKind.OpenBracket, "[");
            var closeBracket = Expect(TokenKind.CloseBracket, "]");

            type = new ArrayTypeSyntax(type, openBracket, closeBracket);
        }

        return type;
    }
    
    private TypeSyntax ParseNonArrayType()
    {
        TypeSyntax? left = ParsePredefinedType();

        left ??= ParseSimpleType();

        while (Current.Kind == TokenKind.Dot)
        {
            var dot = Expect(TokenKind.Dot);

            var right = ParseSimpleType();

            left = new QualifiedTypeSyntax(left, dot, right);
        }

        if (Current.Kind == TokenKind.Asterisk)
            return new PointerTypeSyntax(left, Expect(TokenKind.Asterisk));

        return left;
    }

    private SimpleTypeSyntax ParseSimpleType()
    {
        var identifier = ExpectIdentifier();
        
        return new SimpleTypeSyntax(identifier);
    }

    private PredefinedTypeSyntax? ParsePredefinedType()
    {
        switch (Current.Kind)
        {
            case TokenKind.Object:
            case TokenKind.String:
            case TokenKind.Bool:
            case TokenKind.Byte:
            case TokenKind.Sbyte:
            case TokenKind.Char:
            case TokenKind.Decimal:
            case TokenKind.Double:
            case TokenKind.Float:
            case TokenKind.Int:
            case TokenKind.Uint:
            case TokenKind.Long:
            case TokenKind.Ulong:
            case TokenKind.Ushort:
            case TokenKind.Void:
                return new PredefinedTypeSyntax(Expect(Current.Kind));
        }

        return null;
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
                if (IsTokenValidForDeclaration(declarationContext, Current.Kind))
                    break;

                diagnostics.ReportError(Current.Position,
                    "The compilation unit or namespace contains an invalid declaration or directive");

                isInErrorRecovery = true;
            }

            if (isInErrorRecovery) Synchronize(declarationContext, TokenKind.Using);
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

            if (isInErrorRecovery) Synchronize(declarationContext);
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
            case TokenKind.Struct:
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

            if (isInErrorRecovery && IsTokenValidForDeclaration(declarationContext, Current.Kind))
            {
                isInErrorRecovery = false;

                break;
            }

            if (isInErrorRecovery) Synchronize(declarationContext, TokenKind.Using);
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
            case TokenKind.Struct:
                return ParseStructDeclaration(declarationContext, modifiers);
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


    private StructDeclarationNode ParseStructDeclaration(DeclarationKind declarationContext,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ClassDeclarationNode>(declarationContext, modifiers);

        var classKeyword = Expect(TokenKind.Struct, "struct");
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations(DeclarationKind.Struct);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new StructDeclarationNode(modifiers, classKeyword, identifier, openBrace, declarations, closeBrace);
    }

    private static bool IsTokenValidForDeclaration(DeclarationKind declarationKind, TokenKind tokenKind)
    {
        switch (declarationKind)
        {
            case DeclarationKind.Namespace:
                return tokenKind
                    is TokenKind.Namespace
                    or TokenKind.Class
                    or TokenKind.Struct;
            case DeclarationKind.Class:
            case DeclarationKind.Struct:
                return tokenKind.IsModifier() || tokenKind is TokenKind.Class
                    or TokenKind.Struct;
            default:
                return false;
        }
    }
}