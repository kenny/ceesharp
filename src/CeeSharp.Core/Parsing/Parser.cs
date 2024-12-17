using System.Collections.Immutable;
using CeeSharp.Core.Syntax;
using CeeSharp.Core.Syntax.Nodes;
using CeeSharp.Core.Syntax.Nodes.Declarations;
using CeeSharp.Core.Syntax.Nodes.Expressions;
using CeeSharp.Core.Syntax.Nodes.Statements;
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
        var usings = ParseUsings(ParserContext.Namespace);
        var attributes = ParseAttributes();
        var declarations = ParseNamespaceOrTypeDeclarations(ParserContext.Namespace);

        if (!TryExpect(TokenKind.EndOfFile, out var endOfFile))
            SkipUntilEnd();

        isInErrorRecovery = false;
        skippedTokens.Clear();

        return new CompilationUnitNode(usings, attributes, declarations, endOfFile);
    }

    private SyntaxToken ExpectIdentifier(ParserContext parserContext = ParserContext.None)
    {
        if (!TryExpect(TokenKind.Identifier, out var token, parserContext))
            diagnostics.ReportError(token.EndTextPosition, "Identifier expected");

        return token;
    }

    private SyntaxToken Expect(TokenKind kind)
    {
        _ = TryExpect(kind, out var token);

        return token;
    }

    private OptionalSyntax<SyntaxToken> ExpectIf(TokenKind kind, bool condition, string text,
        ParserContext parserContext = ParserContext.None)
    {
        if (condition)
            return OptionalSyntax.With(Expect(kind, text, parserContext));

        return ExpectOptional(kind);
    }

    private OptionalSyntax<SyntaxToken> ExpectOptional(TokenKind kind)
    {
        var current = Current;

        if (current.Kind != kind)
            return OptionalSyntax<SyntaxToken>.None;

        tokenStream.Advance();

        return OptionalSyntax.With(current);
    }

    private SyntaxToken Expect(TokenKind kind, string text, ParserContext parserContext = ParserContext.None)
    {
        if (!TryExpect(kind, out var token, parserContext))
            diagnostics.ReportError(Previous.EndPosition, $"{text} expected");

        return token;
    }

    private bool TryExpect(TokenKind kind, out SyntaxToken token,
        ParserContext parserContext = ParserContext.None)
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

        if (isInErrorRecovery && parserContext != ParserContext.None)
            Synchronize(parserContext);

        return false;
    }

    private void SkipUntilEnd()
    {
        while (Current.Kind != TokenKind.EndOfFile)
            tokenStream.Advance();

        isInErrorRecovery = false;
    }

    private void Synchronize(ParserContext parserContext, params TokenKind[] synchronizingTokens)
    {
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if ((parserContext != ParserContext.None && IsTokenValidForDeclaration(parserContext, Current.Kind)) ||
                synchronizingTokens.Contains(Current.Kind))
            {
                isInErrorRecovery = false;
                return;
            }

            skippedTokens.Add(new TokenSyntaxTrivia(TriviaKind.SkippedToken, Current, Current.Position));

            tokenStream.Advance();
        }
    }

    private TypeSyntax ParseExpectedType(ParserContext parserContext = ParserContext.None)
    {
        var type = ParseType();

        if (type != null)
            return type;

        diagnostics.ReportError(Current.Position, "Type expected");

        isInErrorRecovery = true;

        var token = new SimpleTypeSyntax(new SyntaxToken(TokenKind.Identifier, "", Current.Position)
        {
            LeadingTrivia = Current.LeadingTrivia.AddRange(skippedTokens)
        });

        skippedTokens.Clear();

        return token;
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

    private ImmutableArray<UsingDirectiveNode> ParseUsings(ParserContext parserContext)
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
                if (IsTokenValidForDeclaration(parserContext, Current.Kind))
                    break;

                diagnostics.ReportError(Current.Position,
                    "The compilation unit or namespace contains an invalid declaration or directive");

                isInErrorRecovery = true;
            }

            if (isInErrorRecovery) Synchronize(parserContext, TokenKind.Using);
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

    private ImmutableArray<AttributeSectionNode> ParseAttributes()
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

        var validTarget = ParseContextualAttributeTarget(Current.Text, out var targetToken);

        var identifier = targetToken switch
        {
            TokenKind.Unknown => Current,
            _ => Current with { Kind = targetToken }
        };

        if (!validTarget)
            diagnostics.ReportWarning(Current.Position, $"'{identifier.Text}' is not a valid attribute target");

        tokenStream.Advance();

        var colon = Expect(TokenKind.Colon, ":");

        return OptionalSyntax.With(new AttributeTargetNode(identifier, colon));
    }

    private bool ParseContextualAttributeTarget(string text, out TokenKind targetToken)
    {
        targetToken = text switch
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

        return targetToken != TokenKind.Unknown;
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

                if (isInErrorRecovery) Synchronize(ParserContext.AttributeList);
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
                if (isInErrorRecovery) Synchronize(ParserContext.AttributeList);

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

    private ImmutableArray<DeclarationNode> ParseNamespaceOrTypeDeclarations(ParserContext parserContext)
    {
        var declarations = ImmutableArray.CreateBuilder<DeclarationNode>();

        while (Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            var declaration = ParseNamespaceOrTypeDeclaration(parserContext);
            if (declaration != null) declarations.Add(declaration);

            if (isInErrorRecovery) Synchronize(parserContext);
        }

        return declarations.ToImmutable();
    }

    private DeclarationNode? ParseNamespaceOrTypeDeclaration(ParserContext parserContext)
    {
        var attributes = ParseAttributes();
        var modifiers = ParseModifiers();

        switch (Current.Kind)
        {
            case TokenKind.Namespace when modifiers.IsEmpty:
                return ParseNamespaceDeclaration();
            case TokenKind.Class:
            case TokenKind.Struct:
            case TokenKind.Enum:
                return ParseTypeDeclaration(parserContext, attributes, modifiers);
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

        if (isInErrorRecovery) Synchronize(ParserContext.Namespace);

        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var usings = ParseUsings(ParserContext.Namespace);
        var declarations = ParseNamespaceOrTypeDeclarations(ParserContext.Namespace);
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

    private void ValidateModifiers<TNode>(ParserContext parserContext, ImmutableArray<SyntaxToken> modifiers)
        where TNode : IModifierValidator
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

            if (!TNode.IsModifierValid(parserContext, modifier.Kind))
                diagnostics.ReportError(modifier.Position,
                    $"The modifier '{modifier.Text}' is not valid for this item");
        }
    }

    private ImmutableArray<DeclarationNode> ParseTypeDeclarations(ParserContext parserContext)
    {
        var declarations = ImmutableArray.CreateBuilder<DeclarationNode>();

        while (!isInErrorRecovery && Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            var attributes = ParseAttributes();
            var modifiers = ParseModifiers();
            var declaration = ParseMemberDeclaration(parserContext, attributes, modifiers);
            if (declaration != null) declarations.Add(declaration);

            if (isInErrorRecovery && IsTokenValidForDeclaration(parserContext, Current.Kind))
            {
                isInErrorRecovery = false;

                // Do not stop processing if we're in the same context
                if (parserContext != ParserContext.Type)
                    break;
            }

            if (isInErrorRecovery) Synchronize(parserContext);
        }

        return declarations.ToImmutable();
    }

    private DeclarationNode? ParseMemberDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        switch (Current.Kind)
        {
            case TokenKind.Class:
            case TokenKind.Struct:
            case TokenKind.Enum:
            case TokenKind.Delegate:
                return ParseTypeDeclaration(parserContext, attributes, modifiers);

            case TokenKind.Identifier when parserContext != ParserContext.Namespace:
                if (Lookahead.Kind != TokenKind.OpenParen)
                {
                    var type = ParseType();

                    if (Lookahead.Kind is TokenKind.Semicolon or TokenKind.Assign or TokenKind.Comma)
                        return ParseFieldDeclaration(parserContext, attributes, modifiers, type!);

                    var explicitInterface = Lookahead.Kind switch
                    {
                        TokenKind.Dot => ParseExplicitInterface(parserContext),
                        _ => OptionalSyntax<ExplicitInterfaceNode>.None
                    };

                    if (!isInErrorRecovery)
                    {
                        if (Current.Kind == TokenKind.This)
                            return ParseIndexerDeclaration(parserContext, attributes, modifiers, type!,
                                explicitInterface);


                        switch (Lookahead.Kind)
                        {
                            case TokenKind.OpenParen:
                                return ParseMethodDeclaration(parserContext, attributes, modifiers, type!,
                                    explicitInterface);
                            case TokenKind.OpenBrace:
                                return ParsePropertyDeclaration(parserContext, attributes, modifiers, type!,
                                    explicitInterface);
                        }
                    }

                    isInErrorRecovery = false;

                    return HandleIncompleteMember(parserContext, attributes, modifiers, type!);
                }

                return ParseConstructorDeclaration(parserContext, attributes, modifiers);
            default:
            {
                if (!Current.Kind.IsPredefinedType())
                    return HandleIncompleteMember(parserContext, attributes, modifiers);

                var predefinedType = ParsePredefinedType();

                if (Lookahead.Kind is TokenKind.Semicolon or TokenKind.Assign or TokenKind.Comma)
                    return ParseFieldDeclaration(parserContext, attributes, modifiers, predefinedType!);

                var explicitInterface = Lookahead.Kind switch
                {
                    TokenKind.Dot => ParseExplicitInterface(parserContext),
                    _ => OptionalSyntax<ExplicitInterfaceNode>.None
                };

                if (Current.Kind == TokenKind.This)
                    return ParseIndexerDeclaration(parserContext, attributes, modifiers, predefinedType!,
                        explicitInterface);

                switch (Lookahead.Kind)
                {
                    case TokenKind.OpenParen:
                        return ParseMethodDeclaration(parserContext, attributes, modifiers, predefinedType!,
                            explicitInterface);
                    case TokenKind.OpenBrace:
                        return ParsePropertyDeclaration(parserContext, attributes, modifiers, predefinedType!,
                            explicitInterface);
                }

                return null;
            }
        }
    }

    private DeclarationNode HandleIncompleteMember(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers, params SyntaxElement[] elements)
    {
        if (!isInErrorRecovery)
        {
            diagnostics.ReportError(Current.Position,
                "Invalid member declaration");
            isInErrorRecovery = true;
        }

        Synchronize(parserContext);

        return new IncompleteMemberDeclarationNode(attributes, modifiers.As<SyntaxElement>().AddRange(elements));
    }

    private DeclarationNode? ParseTypeDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        switch (Current.Kind)
        {
            case TokenKind.Class:
                return ParseClassDeclaration(parserContext, attributes, modifiers);
            case TokenKind.Struct:
                return ParseStructDeclaration(parserContext, attributes, modifiers);
            case TokenKind.Enum:
                return ParseEnumDeclaration(parserContext, attributes, modifiers);
            case TokenKind.Delegate:
                return ParseDelegateDeclaration(parserContext, attributes, modifiers);
            default:
                if (!isInErrorRecovery) isInErrorRecovery = true;

                return null;
        }
    }

    private EnumDeclarationNode ParseEnumDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<EnumDeclarationNode>(parserContext, modifiers);

        var enumKeyword = Expect(TokenKind.Enum, "enum");
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var members = ParseEnumMemberDeclarations();
        var closeBrace = Expect(TokenKind.CloseBrace, "}");
        var semicolon = ExpectOptional(TokenKind.Semicolon);

        return new EnumDeclarationNode(attributes, modifiers, enumKeyword, identifier, openBrace, members, closeBrace,
            semicolon);
    }

    private ImmutableArray<EnumMemberDeclarationNode> ParseEnumMemberDeclarations()
    {
        var members = ImmutableArray.CreateBuilder<EnumMemberDeclarationNode>();

        while (!isInErrorRecovery && Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            var attributes = ParseAttributes();

            var member = ParseEnumMemberDeclaration(attributes);

            members.Add(member);

            if (isInErrorRecovery && IsTokenValidForDeclaration(ParserContext.Type, Current.Kind))
            {
                isInErrorRecovery = false;
                break;
            }

            if (isInErrorRecovery) Synchronize(ParserContext.Type);
        }

        return members.ToImmutable();
    }

    private EnumMemberDeclarationNode ParseEnumMemberDeclaration(ImmutableArray<AttributeSectionNode> attributes)
    {
        var identifier = ExpectIdentifier(ParserContext.EnumMember);
        var assign = ExpectOptional(TokenKind.Assign);
        var expression = assign.HasValue switch
        {
            true => OptionalSyntax.With<ExpressionNode>(new IdentifierExpressionNode(ExpectIdentifier())),
            false => OptionalSyntax<ExpressionNode>.None
        };
        var comma = ExpectIf(TokenKind.Comma, Lookahead.Kind != TokenKind.CloseBrace, ",", ParserContext.EnumMember);

        return new EnumMemberDeclarationNode(attributes, identifier, assign, expression, comma);
    }

    private DelegateDeclarationNode ParseDelegateDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes, ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<DelegateDeclarationNode>(parserContext, modifiers);

        var enumKeyword = Expect(TokenKind.Delegate, "delegate");
        var type = ParseExpectedType();
        var identifier = ExpectIdentifier();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList(parserContext, TokenKind.CloseParen);
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new DelegateDeclarationNode(attributes, modifiers, enumKeyword, type!, identifier, openParen, parameters,
            closeParen, semicolon);
    }

    private ClassDeclarationNode ParseClassDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ClassDeclarationNode>(parserContext, modifiers);

        var classKeyword = Expect(TokenKind.Class, "class");
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations(ParserContext.Type);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new ClassDeclarationNode(attributes, modifiers, classKeyword, identifier, openBrace, declarations,
            closeBrace);
    }

    private StructDeclarationNode ParseStructDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ClassDeclarationNode>(parserContext, modifiers);

        var structKeyword = Expect(TokenKind.Struct, "struct");
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations(ParserContext.Type);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new StructDeclarationNode(attributes, modifiers, structKeyword, identifier, openBrace, declarations,
            closeBrace);
    }

    private MethodDeclarationNode ParseMethodDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers, TypeSyntax returnType,
        OptionalSyntax<ExplicitInterfaceNode> explicitInterface)
    {
        ValidateModifiers<MethodDeclarationNode>(parserContext, modifiers);

        var identifier = ExpectIdentifier();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList(parserContext, TokenKind.CloseParen);
        var closeParen = Expect(TokenKind.CloseParen, ")");

        SyntaxElement blockOrSemicolon = Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(ParserContext.Method),
            _ => Expect(TokenKind.Semicolon, ";")
        };

        if (isInErrorRecovery) isInErrorRecovery = false;

        return new MethodDeclarationNode(
            attributes,
            modifiers,
            returnType,
            explicitInterface,
            identifier,
            openParen,
            parameters,
            closeParen,
            blockOrSemicolon);
    }

    private ConstructorDeclarationNode ParseConstructorDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<MethodDeclarationNode>(parserContext, modifiers);

        var identifier = ExpectIdentifier();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList(parserContext, TokenKind.CloseParen);
        var closeParen = Expect(TokenKind.CloseParen, ")");

        SyntaxElement blockOrSemicolon = Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(ParserContext.Method),
            _ => Expect(TokenKind.Semicolon, ";")
        };

        if (isInErrorRecovery) isInErrorRecovery = false;

        return new ConstructorDeclarationNode(
            attributes,
            modifiers,
            identifier,
            openParen,
            parameters,
            closeParen,
            blockOrSemicolon);
    }

    private FieldDeclarationNode ParseFieldDeclaration(
        ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        TypeSyntax type)
    {
        ValidateModifiers<FieldDeclarationNode>(parserContext, modifiers);

        var declarators = ImmutableArray.CreateBuilder<VariableDeclaratorNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        declarators.Add(ParseVariableDeclarator());

        while (Current.Kind == TokenKind.Comma)
        {
            var comma = Expect(TokenKind.Comma);

            declarators.Add(ParseVariableDeclarator());
            separators.Add(comma);
        }

        var variableDeclarators =
            new SeparatedSyntaxList<VariableDeclaratorNode>(declarators.ToImmutable(), separators.ToImmutable());

        var semicolon = Expect(TokenKind.Semicolon);

        return new FieldDeclarationNode(
            attributes,
            modifiers,
            type,
            variableDeclarators,
            semicolon);
    }

    private VariableDeclaratorNode ParseVariableDeclarator()
    {
        var identifier = ExpectIdentifier();

        var assign = ExpectOptional(TokenKind.Assign);

        var initializer = assign.HasValue switch
        {
            true => OptionalSyntax.With<ExpressionNode>(new IdentifierExpressionNode(ExpectIdentifier())),
            false => OptionalSyntax<ExpressionNode>.None
        };
        return new VariableDeclaratorNode(identifier, assign, initializer);
    }

    private SeparatedSyntaxList<ParameterNode> ParseParameterList(ParserContext parserContext, TokenKind closeToken)
    {
        var parameters = ImmutableArray.CreateBuilder<ParameterNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind != closeToken && Current.Kind != TokenKind.EndOfFile)
        {
            if (parameters.Count > 0)
            {
                separators.Add(Expect(TokenKind.Comma, ","));

                if (isInErrorRecovery && IsTokenValidForDeclaration(parserContext, Current.Kind))
                {
                    isInErrorRecovery = false;
                    break;
                }

                if (isInErrorRecovery) Synchronize(ParserContext.ParameterList);

                if (Current.Kind == closeToken)
                    break;
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

    private PropertyDeclarationNode ParsePropertyDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        TypeSyntax type, OptionalSyntax<ExplicitInterfaceNode> explicitInterface)
    {
        ValidateModifiers<PropertyDeclarationNode>(parserContext, modifiers);

        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var accessors = ParseAccessorDeclarations(ParserContext.Property);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new PropertyDeclarationNode(
            attributes,
            modifiers,
            type,
            explicitInterface,
            identifier,
            openBrace,
            accessors,
            closeBrace);
    }

    private ImmutableArray<AccessorDeclarationNode> ParseAccessorDeclarations(ParserContext parserContext)
    {
        var accessors = ImmutableArray.CreateBuilder<AccessorDeclarationNode>();

        while (Current.Kind is not (TokenKind.CloseBrace or TokenKind.EndOfFile))
        {
            var accessorAttributes = ParseAttributes();
            var accessorModifiers = ParseModifiers();

            var keyword = OptionalSyntax.With(Current.Text switch
            {
                "get" => Current with { Kind = TokenKind.Get },
                "set" => Current with { Kind = TokenKind.Set },
                _ => Current
            });

            var keywordKind = keyword.Element!.Kind;

            if (keywordKind is not (TokenKind.Get or TokenKind.Set))
            {
                diagnostics.ReportError(keyword.Element!.Position, "A get or set accessor expected");

                if (keywordKind is not (TokenKind.OpenBrace or TokenKind.Semicolon))
                {
                    if (IsTokenValidForDeclaration(parserContext, Current.Kind))
                    {
                        accessors.Add(HandleIncompleteAccessor(accessorAttributes, accessorModifiers));

                        break;
                    }

                    isInErrorRecovery = true;

                    Synchronize(ParserContext.Property);

                    continue;
                }

                keyword = OptionalSyntax<SyntaxToken>.None;
            }

            accessors.Add(ParseAccessorDeclaration(accessorAttributes, accessorModifiers, keyword));

            if (isInErrorRecovery)
                Synchronize(ParserContext.Property);

            if (Current.Kind != TokenKind.Identifier && IsTokenValidForDeclaration(parserContext, Current.Kind))
                break;
        }

        return accessors.ToImmutable();
    }

    private IndexerDeclarationNode ParseIndexerDeclaration(ParserContext parserContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        TypeSyntax type, OptionalSyntax<ExplicitInterfaceNode> explicitInterface)
    {
        ValidateModifiers<IndexerDeclarationNode>(parserContext, modifiers);

        var thisKeyword = Expect(TokenKind.This, "this");
        var openBracket = Expect(TokenKind.OpenBracket, "[");
        var parameters = ParseParameterList(parserContext, TokenKind.CloseBracket);
        var closeBracket = Expect(TokenKind.CloseBracket, "]");
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var accessors = ParseAccessorDeclarations(parserContext);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new IndexerDeclarationNode(attributes, modifiers, type, explicitInterface, thisKeyword, openBracket,
            parameters, closeBracket, openBrace, accessors, closeBrace);
    }

    private AccessorDeclarationNode HandleIncompleteAccessor(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        return new AccessorDeclarationNode(attributes, modifiers,
            OptionalSyntax<SyntaxToken>.None,
            OptionalSyntax<BlockStatementNode>.None,
            OptionalSyntax<SyntaxToken>.None);
    }

    private SimpleNameNode ParseSimpleName(ParserContext parserContext)
    {
        var identifier = ExpectIdentifier(parserContext);

        return new SimpleNameNode(identifier);
    }


    private MemberNameNode ParseQualifiedName(ParserContext parserContext)
    {
        MemberNameNode left = ParseSimpleName(parserContext);

        while (Current.Kind == TokenKind.Dot)
        {
            if (tokenStream.Peek(2).Kind is not (TokenKind.Identifier or TokenKind.Dot))
                break;

            var dot = Expect(TokenKind.Dot);

            var right = ParseSimpleName(parserContext);

            left = new QualifiedNameNode(left, dot, right);
        }

        return left;
    }


    private OptionalSyntax<ExplicitInterfaceNode> ParseExplicitInterface(ParserContext parserContext)
    {
        if (Lookahead.Kind != TokenKind.Dot)
            return OptionalSyntax<ExplicitInterfaceNode>.None;

        var name = ParseQualifiedName(parserContext);
        var dot = Expect(TokenKind.Dot, ".");

        return new ExplicitInterfaceNode(name, dot);
    }

    private AccessorDeclarationNode ParseAccessorDeclaration(
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        OptionalSyntax<SyntaxToken> keyword)
    {
        if (keyword.HasValue)
            tokenStream.Advance();

        var body = OptionalSyntax<BlockStatementNode>.None;
        var semicolon = OptionalSyntax<SyntaxToken>.None;

        if (Current.Kind == TokenKind.OpenBrace)
            body = OptionalSyntax.With(ParseBlockStatement(ParserContext.Method));
        else
            semicolon = OptionalSyntax.With(Expect(TokenKind.Semicolon, ";"));

        return new AccessorDeclarationNode(attributes, modifiers, keyword, body, semicolon);
    }

    private StatementNode ParseStatement(ParserContext parserContext)
    {
        return Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(parserContext),
            TokenKind.If => ParseIfStatement(parserContext),
            _ => ParseExpressionStatement()
        };
    }

    private BlockStatementNode ParseBlockStatement(ParserContext parserContext)
    {
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var statements = ImmutableArray.CreateBuilder<StatementNode>();

        while (Current.Kind != TokenKind.CloseBrace && Current.Kind != TokenKind.EndOfFile)
        {
            var statement = ParseStatement(parserContext);
            statements.Add(statement);

            if (isInErrorRecovery)
            {
                Synchronize(parserContext, TokenKind.Semicolon, TokenKind.CloseBrace);
                
                if (Current.Kind == TokenKind.CloseBrace)
                    break;
            }
        }

        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new BlockStatementNode(openBrace, statements.ToImmutable(), closeBrace);
    }

    private StatementNode ParseExpressionStatement()
    {
        var expression = ParseExpression();
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new ExpressionStatementNode(expression, semicolon);
    }

    private IfStatementNode ParseIfStatement(ParserContext parserContext)
    {
        var ifKeyword = Expect(TokenKind.If, "if");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var condition = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var statement = ParseStatement(parserContext);
        var elseClause = ParseElseClause(parserContext);

        return new IfStatementNode(ifKeyword, openParen, condition, closeParen, statement, elseClause);
    }

    private OptionalSyntax<ElseClauseNode> ParseElseClause(ParserContext parserContext)
    {
        if (Current.Kind != TokenKind.Else)
            return OptionalSyntax<ElseClauseNode>.None;

        var elseKeyword = Expect(TokenKind.Else, "else");
        var statement = ParseStatement(parserContext);

        return OptionalSyntax.With(new ElseClauseNode(elseKeyword, statement));
    }
    
    private ExpressionNode ParseExpression()
    {
        return ParseAssignmentExpression();
    }

    private ExpressionNode ParseAssignmentExpression()
    {
        var left = ParseConditionalExpression();

        if (!Current.Kind.IsAssignmentOperator())
            return left;

        var operatorToken = Expect(Current.Kind);
        var right = ParseAssignmentExpression();

        return new AssignmentExpressionNode(left, operatorToken, right);
    }

    private ExpressionNode ParseConditionalExpression()
    {
        var condition = ParseLogicalOrExpression();

        if (Current.Kind != TokenKind.Question) 
            return condition;
        
        var questionToken = Expect(TokenKind.Question);
        var ifTrue = ParseExpression();
        var colonToken = Expect(TokenKind.Colon, ":");
        var ifFalse = ParseExpression();
            
        return new ConditionalExpressionNode(condition, questionToken, ifTrue, colonToken, ifFalse);
    }
    
    private ExpressionNode ParseLogicalOrExpression()
    {
        var left = ParseLogicalAndExpression();

        while (Current.Kind == TokenKind.OrOr)
        {
            var operatorToken = Expect(TokenKind.OrOr);
            var right = ParseLogicalAndExpression();
            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionNode ParseLogicalAndExpression()
    {
        var left = ParseEqualityExpression();

        while (Current.Kind == TokenKind.AndAnd)
        {
            var operatorToken = Expect(TokenKind.AndAnd);
            var right = ParseEqualityExpression();
            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionNode ParseEqualityExpression()
    {
        var left = ParseRelationalExpression();

        while (Current.Kind.IsEqualityOperator())
        {
            var operatorToken = Expect(Current.Kind);
            var right = ParseRelationalExpression();
            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionNode ParseRelationalExpression()
    {
        var left = ParseAdditiveExpression();

        while (true)
        {
            switch (Current.Kind)
            {
                case TokenKind.Is:
                    var isKeyword = Expect(TokenKind.Is);
                    var isType = ParseExpectedType();
                    left = new IsExpressionNode(left, isKeyword, isType);
                    break;
                
                case TokenKind.As:
                    var asKeyword = Expect(TokenKind.As);
                    var asType = ParseExpectedType();
                    left = new AsExpressionNode(left, asKeyword, asType);
                    break;

                default:
                    if (!Current.Kind.IsRelationalOperator())
                        return left;
                    
                    var operatorToken = Expect(Current.Kind);
                    var right = ParseAdditiveExpression();
                    left = new BinaryExpressionNode(left, operatorToken, right);
                    break;
            }
        }
    }
    
    private ExpressionNode ParseAdditiveExpression()
    {
        var left = ParseMultiplicativeExpression();

        while (Current.Kind.IsAdditiveOperator())
        {
            var operatorToken = Expect(Current.Kind);
            var right = ParseMultiplicativeExpression();
            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionNode ParseMultiplicativeExpression()
    {
        var left = ParseUnaryExpression();

        while (Current.Kind.IsMultiplicativeOperator())
        {
            var operatorToken = Expect(Current.Kind);
            var right = ParseUnaryExpression();
            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionNode ParseUnaryExpression()
    {
        if (Current.Kind.IsUnaryOperator())
        {
            var operatorToken = Expect(Current.Kind);
            var operand = ParseUnaryExpression();
            return new PrefixUnaryExpressionNode(operatorToken, operand);
        }

        var expression = ParsePrimaryExpression();

        while (Current.Kind is TokenKind.PlusPlus or TokenKind.MinusMinus)
        {
            var operatorToken = Expect(Current.Kind);
            expression = new PostfixUnaryExpressionNode(expression, operatorToken);
        }

        return expression;
    }
    
    private ExpressionNode ParsePrimaryExpression()
    {
        return new IdentifierExpressionNode(ExpectIdentifier());
    }

    private static bool IsTokenValidForDeclaration(ParserContext parserContext, TokenKind tokenKind)
    {
        return parserContext switch
        {
            ParserContext.Namespace => tokenKind.IsModifier() ||
                                       tokenKind is TokenKind.Namespace or TokenKind.Class or TokenKind.Struct
                                           or TokenKind.Enum
                                           or TokenKind.OpenBracket,
            ParserContext.Type => tokenKind.IsModifier() || tokenKind.IsPredefinedType() ||
                                  tokenKind is TokenKind.Class or TokenKind.Struct or TokenKind.Enum
                                      or TokenKind.Identifier or TokenKind.CloseBrace,
            ParserContext.Delegate => tokenKind.IsPredefinedType() || tokenKind.IsParameterModifier() ||
                                      tokenKind is TokenKind.Identifier,
            ParserContext.ParameterList => tokenKind.IsPredefinedType() || tokenKind.IsParameterModifier() ||
                                           tokenKind is TokenKind.Identifier or TokenKind.CloseParen,
            ParserContext.AttributeList => tokenKind is TokenKind.Identifier or TokenKind.Comma
                or TokenKind.CloseBracket
                or TokenKind.CloseParen,
            ParserContext.EnumMember => tokenKind is TokenKind.Identifier or TokenKind.CloseBrace
                or TokenKind.CloseBracket
                or TokenKind.CloseParen,
            ParserContext.Property => tokenKind is TokenKind.Get or TokenKind.Set or TokenKind.OpenBrace
                or TokenKind.CloseBrace,
            _ => false
        };
    }
}