using System.Collections.Immutable;
using CeeSharp.Core.Syntax;
using CeeSharp.Core.Syntax.Nodes;
using CeeSharp.Core.Syntax.Nodes.Declarations;
using CeeSharp.Core.Syntax.Nodes.Expressions;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Parsing;

public sealed class Parser(Diagnostics diagnostics, TokenStream tokenStream)
{
    private readonly ImmutableArray<SyntaxTrivia>.Builder skippedTokens = ImmutableArray.CreateBuilder<SyntaxTrivia>();
    private bool isInErrorRecovery;

    private SyntaxToken Current => tokenStream.Current;

    private SyntaxToken Lookahead => tokenStream.Lookahead;
    private SyntaxToken Previous => tokenStream.Previous;

    public CompilationUnitNode Parse()
    {
        var usings = ParseUsings(DeclarationKind.Namespace);
        var attributes = ParseAttributeSections();
        var declarations = ParseNamespaceOrTypeDeclarations(DeclarationKind.Namespace);

        if (!TryExpect(TokenKind.EndOfFile, out var endOfFile))
            SkipUntilEnd();

        isInErrorRecovery = false;
        skippedTokens.Clear();

        return new CompilationUnitNode(usings, attributes, declarations, endOfFile);
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

        token = new SyntaxToken(kind, "", Previous.EndPosition)
        {
            LeadingTrivia = Current.LeadingTrivia.AddRange(skippedTokens)
        };

        skippedTokens.Clear();

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
            if ((context != DeclarationKind.None && IsTokenValidForDeclaration(context, Current.Kind)) ||
                synchronizingTokens.Contains(Current.Kind))
            {
                isInErrorRecovery = false;
                return;
            }

            skippedTokens.Add(new TokenSyntaxTrivia(TriviaKind.SkippedToken, Current, Current.Position));

            tokenStream.Advance();
        }
    }

    private TypeSyntax ParseExpectedType()
    {
        var type = ParseType();

        if (type != null)
            return type;

        diagnostics.ReportError(Current.Position, "Type expected");

        isInErrorRecovery = true;

        return new SimpleTypeSyntax(new SyntaxToken(TokenKind.Identifier, "", Current.Position));
    }

    private TypeSyntax? ParseType()
    {
        var type = ParseNonArrayType();

        if (type == null)
            return null;

        while (Current.Kind == TokenKind.OpenBracket)
        {
            var openBracket = Expect(TokenKind.OpenBracket, "[");
            var closeBracket = Expect(TokenKind.CloseBracket, "]");

            type = new ArrayTypeSyntax(type, openBracket, closeBracket);
        }

        return type;
    }

    private TypeSyntax? ParseNonArrayType()
    {
        TypeSyntax? left = ParsePredefinedType();

        left ??= ParseQualifiedType();

        if (left == null)
            return null;

        if (Current.Kind == TokenKind.Asterisk)
            return new PointerTypeSyntax(left, Expect(TokenKind.Asterisk));

        return left;
    }

    private TypeSyntax? ParseQualifiedType()
    {
        TypeSyntax? left = ParseSimpleType();

        if (left == null)
            return null;

        while (Current.Kind == TokenKind.Dot)
        {
            var dot = Expect(TokenKind.Dot);

            var right = ParseSimpleTypeExact();

            left = new QualifiedTypeSyntax(left, dot, right);
        }

        return left;
    }

    private TypeSyntax ParseQualifiedTypeExact()
    {
        var type = ParseQualifiedType();

        if (type != null)
            return type;

        return ParseSimpleTypeExact();
    }

    private SimpleTypeSyntax? ParseSimpleType()
    {
        if (!TryExpect(TokenKind.Identifier, out var identifier))
            return null;

        return new SimpleTypeSyntax(identifier);
    }

    private SimpleTypeSyntax ParseSimpleTypeExact()
    {
        var identifier = ExpectIdentifier();

        return new SimpleTypeSyntax(identifier);
    }

    private PredefinedTypeSyntax? ParsePredefinedType()
    {
        if (Current.Kind.IsPredefinedType())
            return new PredefinedTypeSyntax(Expect(Current.Kind));

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
        var alias = ParseUsingAlias();
        var identifier = ExpectIdentifier();
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new UsingDirectiveNode(usingKeyword, alias, identifier, semicolon);
    }

    private OptionalSyntax<UsingAliasNode> ParseUsingAlias()
    {
        if (Lookahead.Kind != TokenKind.Assign)
            return OptionalSyntax<UsingAliasNode>.None;

        var identifier = ExpectIdentifier();
        var assign = Expect(TokenKind.Assign, "=");

        return OptionalSyntax.With(new UsingAliasNode(identifier, assign));
    }

    private ImmutableArray<AttributeSectionNode> ParseAttributeSections()
    {
        var attributes = ImmutableArray.CreateBuilder<AttributeSectionNode>();

        while (Current.Kind == TokenKind.OpenBracket) attributes.Add(ParseAttributeSection());

        return attributes.ToImmutable();
    }

    private AttributeSectionNode ParseAttributeSection()
    {
        var openBracket = Expect(TokenKind.OpenBracket, "[");
        var target = ParseAttributeTarget();
        var attributeList = ParseAttributeList();
        var closeBracket = Expect(TokenKind.CloseBracket, "]");

        return new AttributeSectionNode(openBracket, target, attributeList, closeBracket);
    }

    private OptionalSyntax<AttributeTargetNode> ParseAttributeTarget()
    {
        if (Lookahead.Kind != TokenKind.Colon)
            return OptionalSyntax<AttributeTargetNode>.None;

        var identifier = ParseContextualAttributeTarget(Current.Text, out var isValidTarget) switch
        {
            TokenKind.Unknown => Current,
            var targetToken => Current with { Kind = targetToken }
        };

        if (!isValidTarget)
            diagnostics.ReportWarning(Current.Position, $"'{identifier.Text}' is an invalid attribute target");

        tokenStream.Advance();

        var colon = Expect(TokenKind.Colon, ":");

        return OptionalSyntax.With(new AttributeTargetNode(identifier, colon));
    }

    private TokenKind ParseContextualAttributeTarget(string text, out bool isValidTarget)
    {
        var targetKind = text switch
        {
            "assembly" => TokenKind.Assembly,
            "field" => TokenKind.Field,
            "event" => TokenKind.Event,
            "method" => TokenKind.Method,
            "module" => TokenKind.Module,
            "param" => TokenKind.Param,
            "property" => TokenKind.Property,
            "return" => TokenKind.Return,
            "type" => TokenKind.Type,
            _ => TokenKind.Unknown
        };

        isValidTarget = targetKind != TokenKind.Unknown;

        return targetKind;
    }

    private SeparatedSyntaxList<AttributeNode> ParseAttributeList()
    {
        var attributes = ImmutableArray.CreateBuilder<AttributeNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (attributes.Count > 0)
            {
                if (Current.Kind != TokenKind.Comma)
                    break;

                separators.Add(Expect(TokenKind.Comma, ","));

                if (isInErrorRecovery) Synchronize(DeclarationKind.AttributeList);
            }

            attributes.Add(ParseAttribute());
        }

        return new SeparatedSyntaxList<AttributeNode>(attributes.ToImmutable(), separators.ToImmutable());
    }

    private AttributeNode ParseAttribute()
    {
        var name = ParseQualifiedTypeExact();
        var arguments = ParseAttributeArguments();

        return new AttributeNode(name, arguments);
    }

    private OptionalSyntax<AttributeArgumentListNode> ParseAttributeArguments()
    {
        if (Current.Kind != TokenKind.OpenParen)
            return OptionalSyntax<AttributeArgumentListNode>.None;

        var openParen = Expect(TokenKind.OpenParen, "(");
        
        var arguments = ImmutableArray.CreateBuilder<AttributeArgumentNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind != TokenKind.CloseParen && Current.Kind != TokenKind.EndOfFile)
        {
            if (arguments.Count > 0)
            {
                if (isInErrorRecovery) Synchronize(DeclarationKind.AttributeList);

                if (Current.Kind != TokenKind.Comma)
                    break;

                separators.Add(Expect(TokenKind.Comma, ","));
            }

            arguments.Add(ParseAttributeArgument());
        }

        var argumentWithCommas =
            new SeparatedSyntaxList<AttributeArgumentNode>(arguments.ToImmutable(), separators.ToImmutable());
        var closeParen = Expect(TokenKind.CloseParen, ")");

        return OptionalSyntax.With(new AttributeArgumentListNode(openParen, argumentWithCommas, closeParen));
    }

    private AttributeArgumentNode ParseAttributeArgument()
    {
        var named = ParseAttributeNamedArgument();
        var expression = new IdentifierExpressionNode(ExpectIdentifier());

        return new AttributeArgumentNode(named, expression);
    }

    private OptionalSyntax<AttributeNamedArgumentNode> ParseAttributeNamedArgument()
    {
        if (Lookahead.Kind != TokenKind.Assign)
            return OptionalSyntax<AttributeNamedArgumentNode>.None;

        var identifier = ExpectIdentifier();
        var assign = Expect(TokenKind.Assign, "=");

        return OptionalSyntax.With(new AttributeNamedArgumentNode(identifier, assign));
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
        var name = ParseQualifiedTypeExact();

        if (isInErrorRecovery) Synchronize(DeclarationKind.Namespace);

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
            var declaration = ParseMemberDeclaration(declarationContext, modifiers);
            if (declaration != null) declarations.Add(declaration);

            if (isInErrorRecovery && IsTokenValidForDeclaration(declarationContext, Current.Kind))
            {
                isInErrorRecovery = false;
                break;
            }

            if (isInErrorRecovery) Synchronize(declarationContext);
        }

        return declarations.ToImmutable();
    }

    private DeclarationNode? ParseMemberDeclaration(DeclarationKind declarationContext,
        ImmutableArray<SyntaxToken> modifiers)
    {
        switch (Current.Kind)
        {
            case TokenKind.Class:
            case TokenKind.Struct:
                return ParseTypeDeclaration(declarationContext, modifiers);

            case TokenKind.Identifier when declarationContext != DeclarationKind.Namespace:
                if (Lookahead.Kind != TokenKind.OpenParen)
                {
                    var type = ParseType();

                    if (!isInErrorRecovery)
                        return ParseMethodDeclaration(declarationContext, modifiers, type!);

                    isInErrorRecovery = false;

                    return HandleIncompleteMember(declarationContext, modifiers, type!);
                }

                return ParseConstructorDeclaration(declarationContext, modifiers);

            case TokenKind.Void:
            case TokenKind.Int:
            case TokenKind.String:
            case TokenKind.Bool:
            case TokenKind.Double:
            case TokenKind.Float:
            case TokenKind.Long:
            case TokenKind.Short:
            case TokenKind.Byte:
            case TokenKind.Char:
            case TokenKind.Decimal:
            case TokenKind.Object:
                var predefinedType = ParsePredefinedType();
                return ParseMethodDeclaration(declarationContext, modifiers, predefinedType!);
            default:
                return HandleIncompleteMember(declarationContext, modifiers);
        }
    }

    private DeclarationNode HandleIncompleteMember(DeclarationKind declarationContext,
        ImmutableArray<SyntaxToken> modifiers, params SyntaxElement[] elements)
    {
        if (!isInErrorRecovery)
        {
            diagnostics.ReportError(Current.Position,
                "Invalid member declaration");
            isInErrorRecovery = true;
        }

        Synchronize(declarationContext);

        return new IncompleteMemberDeclarationNode(modifiers.As<SyntaxElement>().AddRange(elements));
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

    private MethodDeclarationNode ParseMethodDeclaration(DeclarationKind declarationContext,
        ImmutableArray<SyntaxToken> modifiers, TypeSyntax returnType)
    {
        ValidateModifiers<MethodDeclarationNode>(declarationContext, modifiers);

        var identifier = ExpectIdentifier();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList();
        var closeParen = Expect(TokenKind.CloseParen, ")");

        SyntaxElement blockOrSemicolon = Current.Kind switch
        {
            TokenKind.OpenBrace => ParseMethodBody(),
            _ => Expect(TokenKind.Semicolon, ";")
        };

        if (isInErrorRecovery) isInErrorRecovery = false;

        return new MethodDeclarationNode(
            modifiers,
            returnType,
            identifier,
            openParen,
            parameters,
            closeParen,
            blockOrSemicolon);
    }

    private ConstructorDeclarationNode ParseConstructorDeclaration(DeclarationKind declarationContext,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<MethodDeclarationNode>(declarationContext, modifiers);

        var identifier = ExpectIdentifier();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList();
        var closeParen = Expect(TokenKind.CloseParen, ")");

        SyntaxElement blockOrSemicolon = Current.Kind switch
        {
            TokenKind.OpenBrace => ParseMethodBody(),
            _ => Expect(TokenKind.Semicolon, ";")
        };

        if (isInErrorRecovery) isInErrorRecovery = false;

        return new ConstructorDeclarationNode(
            modifiers,
            identifier,
            openParen,
            parameters,
            closeParen,
            blockOrSemicolon);
    }

    private BlockNode ParseMethodBody()
    {
        var openBrace = Expect(TokenKind.OpenBrace, "{");

        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new BlockNode(openBrace, closeBrace);
    }

    private SeparatedSyntaxList<ParameterNode> ParseParameterList()
    {
        var parameters = ImmutableArray.CreateBuilder<ParameterNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind != TokenKind.CloseParen && Current.Kind != TokenKind.EndOfFile)
        {
            if (parameters.Count > 0)
            {
                separators.Add(Expect(TokenKind.Comma, ","));

                if (isInErrorRecovery) Synchronize(DeclarationKind.ParameterList);
            }

            var parameter = ParseParameter();

            parameters.Add(parameter);
        }

        return new SeparatedSyntaxList<ParameterNode>(parameters.ToImmutable(), separators.ToImmutable());
    }

    private ParameterNode ParseParameter()
    {
        var modifiers = ParseParameterModifiers();
        var type = ParseExpectedType();

        var identifier = ExpectIdentifier();

        return new ParameterNode(modifiers, type, identifier);
    }

    private ImmutableArray<SyntaxToken> ParseParameterModifiers()
    {
        var modifiers = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind.IsParameterModifier())
        {
            modifiers.Add(Current);
            tokenStream.Advance();
        }

        return modifiers.ToImmutable();
    }

    private static bool IsTokenValidForDeclaration(DeclarationKind declarationKind, TokenKind tokenKind)
    {
        switch (declarationKind)
        {
            case DeclarationKind.Namespace:
                return tokenKind.IsModifier() ||
                       tokenKind is TokenKind.Namespace
                           or TokenKind.Class
                           or TokenKind.Struct
                           or TokenKind.OpenBracket;
            case DeclarationKind.Class:
            case DeclarationKind.Struct:
                return tokenKind.IsModifier() || tokenKind.IsPredefinedType() ||
                       tokenKind is TokenKind.Class
                           or TokenKind.Struct
                           or TokenKind.Identifier
                           or TokenKind.CloseBrace;
            case DeclarationKind.ParameterList:
                return tokenKind.IsPredefinedType() || tokenKind.IsParameterModifier() ||
                       tokenKind is TokenKind.Identifier;
            case DeclarationKind.AttributeList:
                return tokenKind is TokenKind.Identifier or TokenKind.Comma or TokenKind.CloseParen;
            default:
                return false;
        }
    }
}