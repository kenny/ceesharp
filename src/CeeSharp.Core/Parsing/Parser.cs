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
    private readonly Stack<ParserContext> contextStack = [];
    private readonly ImmutableArray<SyntaxTrivia>.Builder skippedTokens = ImmutableArray.CreateBuilder<SyntaxTrivia>();
    private ParserContext currentContext = ParserContext.None;
    private bool isInErrorRecovery;

    private SyntaxToken Current => tokenStream.Current;

    private SyntaxToken Lookahead => tokenStream.Lookahead;
    private SyntaxToken Previous => tokenStream.Previous;

    public CompilationUnitNode Parse()
    {
        using var _ = PushContext(ParserContext.Namespace);

        var usings = ParseUsings();
        var attributes = ParseAttributes();
        var declarations = ParseNamespaceOrTypeDeclarations();

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

    private OptionalSyntax<SyntaxToken> ExpectIf(TokenKind kind, bool condition, string text)
    {
        if (condition)
            return OptionalSyntax.With(Expect(kind, text));

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

    private SyntaxToken Expect(TokenKind kind, string text)
    {
        if (!TryExpect(kind, out var token))
            diagnostics.ReportError(Previous.EndPosition, $"{text} expected");

        return token;
    }

    private SyntaxToken SynthesizeToken(TokenKind kind)
    {
        return new SyntaxToken(kind, "", Previous.EndPosition);
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

    private void Synchronize(params TokenKind[] synchronizingTokens)
    {
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if ((currentContext != ParserContext.None && IsTokenValidInContext(Current.Kind)) ||
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

        if (Current.Kind != TokenKind.OpenBracket)
            return type;

        var rankSpecifiers = ImmutableArray.CreateBuilder<ArrayRankSpecifierNode>();

        var isValidType = true;

        do
        {
            var rankSpecifier = ParseArrayRankSpecifier();

            rankSpecifiers.Add(rankSpecifier);

            if (!isValidType)
                continue;

            foreach (var size in rankSpecifier.Sizes.Elements)
                if (size is not EmptyExpressionNode)
                    isValidType = false;
        } while (Current.Kind == TokenKind.OpenBracket);

        return new ArrayTypeSyntax(type, rankSpecifiers.ToImmutable())
        {
            IsValidType = isValidType
        };
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

    private ImmutableArray<UsingDirectiveNode> ParseUsings()
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
                if (IsTokenValidInPrecedingContext(Current.Kind))
                    break;

                diagnostics.ReportError(Current.Position,
                    "The compilation unit or namespace contains an invalid declaration or directive");

                isInErrorRecovery = true;
            }

            if (isInErrorRecovery) Synchronize(TokenKind.Using);
        }

        return usings.ToImmutable();
    }

    private UsingDirectiveNode ParseUsing()
    {
        var usingKeyword = Expect(TokenKind.Using, "using");
        var alias = ParseUsingAlias();
        var name = ParseQualifiedName();
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new UsingDirectiveNode(usingKeyword, alias, name, semicolon);
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
        using var _ = PushContext(ParserContext.AttributeList);

        var attributes = ImmutableArray.CreateBuilder<AttributeNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (attributes.Count > 0)
            {
                if (Current.Kind != TokenKind.Comma)
                    break;

                separators.Add(Expect(TokenKind.Comma, ","));

                if (isInErrorRecovery) Synchronize();
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
                if (isInErrorRecovery) Synchronize();

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
        var expression = ParseExpression();

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

    private ImmutableArray<DeclarationNode> ParseNamespaceOrTypeDeclarations()
    {
        var declarations = ImmutableArray.CreateBuilder<DeclarationNode>();

        while (Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            var declaration = ParseNamespaceOrTypeDeclaration();
            if (declaration != null) declarations.Add(declaration);

            if (isInErrorRecovery) Synchronize();
        }

        return declarations.ToImmutable();
    }

    private DeclarationNode? ParseNamespaceOrTypeDeclaration()
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
            case TokenKind.Interface:
            case TokenKind.Delegate:
                return ParseTypeDeclaration(attributes, modifiers);
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
        using var _ = PushContext(ParserContext.Namespace);

        var namespaceKeyword = Expect(TokenKind.Namespace, "namespace");
        var name = ParseQualifiedTypeExact();

        if (isInErrorRecovery) Synchronize();

        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var usings = ParseUsings();
        var declarations = ParseNamespaceOrTypeDeclarations();
        var closeBrace = Expect(TokenKind.CloseBrace, "}");
        var semicolon = ExpectOptional(TokenKind.Semicolon);

        return new NamespaceDeclarationNode(namespaceKeyword, name, openBrace, usings, declarations, closeBrace,
            semicolon);
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

    private void ValidateModifiers<TNode>(ImmutableArray<SyntaxToken> modifiers)
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

            if (!TNode.IsModifierValid(currentContext, modifier.Kind))
                diagnostics.ReportError(modifier.Position,
                    $"The modifier '{modifier.Text}' is not valid for this item");
        }
    }

    private ImmutableArray<DeclarationNode> ParseTypeDeclarations()
    {
        var declarations = ImmutableArray.CreateBuilder<DeclarationNode>();

        while (!isInErrorRecovery && Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            var attributes = ParseAttributes();
            var modifiers = ParseModifiers();
            var declaration = ParseMemberDeclaration(attributes, modifiers);
            if (declaration != null) declarations.Add(declaration);

            if (isInErrorRecovery && IsTokenValidInPrecedingContext(Current.Kind))
            {
                isInErrorRecovery = false;

                // Do not stop processing if we're in the same context
                if (currentContext != ParserContext.Type)
                    break;
            }

            if (isInErrorRecovery) Synchronize();
        }

        return declarations.ToImmutable();
    }

    private DeclarationNode? ParseMemberDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        switch (Current.Kind)
        {
            case TokenKind.Class:
            case TokenKind.Struct:
            case TokenKind.Enum:
            case TokenKind.Interface:
            case TokenKind.Delegate:
                return ParseTypeDeclaration(attributes, modifiers);

            case TokenKind.Implicit or TokenKind.Explicit:
                return ParseConversionOperatorDeclaration(attributes, modifiers);

            case TokenKind.Const:
                return ParseConstantFieldDeclaration(attributes, modifiers);

            case TokenKind.Event:
                return ParseEventDeclaration(attributes, modifiers);

            case TokenKind.Tilde:
                return ParseDestructorDeclaration(attributes, modifiers);

            case TokenKind.Identifier when currentContext != ParserContext.Namespace:
                if (Lookahead.Kind != TokenKind.OpenParen)
                {
                    var type = ParseType();

                    if (Lookahead.Kind is TokenKind.Semicolon or TokenKind.Assign or TokenKind.Comma)
                        return ParseFieldDeclaration(attributes, modifiers, type!);

                    var explicitInterface = Lookahead.Kind switch
                    {
                        TokenKind.Dot => ParseExplicitInterface(),
                        _ => OptionalSyntax<ExplicitInterfaceNode>.None
                    };

                    if (!isInErrorRecovery)
                        switch (Current.Kind)
                        {
                            case TokenKind.This:
                                return ParseIndexerDeclaration(attributes, modifiers, type!,
                                    explicitInterface);
                            case TokenKind.Operator:
                                return ParseOperatorDeclaration(attributes, modifiers, type!);
                            default:
                                switch (Lookahead.Kind)
                                {
                                    case TokenKind.OpenParen:
                                        return ParseMethodDeclaration(attributes, modifiers, type!,
                                            explicitInterface);
                                    case TokenKind.OpenBrace:
                                        return ParsePropertyDeclaration(attributes, modifiers, type!,
                                            explicitInterface);
                                }

                                break;
                        }

                    isInErrorRecovery = false;

                    return HandleIncompleteMember(attributes, modifiers, type!);
                }

                return ParseConstructorDeclaration(attributes, modifiers);
            default:
            {
                if (!Current.Kind.IsPredefinedType())
                    return HandleIncompleteMember(attributes, modifiers);

                var predefinedType = ParseType();

                if (Lookahead.Kind is TokenKind.Semicolon or TokenKind.Assign or TokenKind.Comma)
                    return ParseFieldDeclaration(attributes, modifiers, predefinedType!);

                var explicitInterface = Lookahead.Kind switch
                {
                    TokenKind.Dot => ParseExplicitInterface(),
                    _ => OptionalSyntax<ExplicitInterfaceNode>.None
                };

                switch (Current.Kind)
                {
                    case TokenKind.This:
                        return ParseIndexerDeclaration(attributes, modifiers, predefinedType!,
                            explicitInterface);
                    case TokenKind.Operator:
                        return ParseOperatorDeclaration(attributes, modifiers, predefinedType!);
                    default:
                        switch (Lookahead.Kind)
                        {
                            case TokenKind.OpenParen:
                                return ParseMethodDeclaration(attributes, modifiers, predefinedType!,
                                    explicitInterface);
                            case TokenKind.OpenBrace:
                                return ParsePropertyDeclaration(attributes, modifiers, predefinedType!,
                                    explicitInterface);
                        }

                        return null;
                }
            }
        }
    }

    private DeclarationNode HandleIncompleteMember(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers, params SyntaxElement[] elements)
    {
        if (!isInErrorRecovery)
        {
            diagnostics.ReportError(Current.Position,
                "Invalid member declaration");
            isInErrorRecovery = true;
        }

        Synchronize();

        return new IncompleteMemberDeclarationNode(attributes, modifiers.As<SyntaxElement>().AddRange(elements));
    }

    private DeclarationNode? ParseTypeDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        using var _ = PushContext(ParserContext.Type);

        switch (Current.Kind)
        {
            case TokenKind.Class:
                return ParseClassDeclaration(attributes, modifiers);
            case TokenKind.Struct:
                return ParseStructDeclaration(attributes, modifiers);
            case TokenKind.Enum:
                return ParseEnumDeclaration(attributes, modifiers);
            case TokenKind.Interface:
                return ParseInterfaceDeclaration(attributes, modifiers);
            case TokenKind.Delegate:
                return ParseDelegateDeclaration(attributes, modifiers);
            default:
                if (!isInErrorRecovery) isInErrorRecovery = true;

                return null;
        }
    }

    private EnumDeclarationNode ParseEnumDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<EnumDeclarationNode>(modifiers);

        var enumKeyword = Expect(TokenKind.Enum, "enum");
        var identifier = ExpectIdentifier();
        var baseType = ParseEnumBaseType();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var members = ParseEnumMemberDeclarations();
        var closeBrace = Expect(TokenKind.CloseBrace, "}");
        var semicolon = ExpectOptional(TokenKind.Semicolon);

        return new EnumDeclarationNode(attributes, modifiers, enumKeyword, identifier, baseType, openBrace, members,
            closeBrace,
            semicolon);
    }

    private OptionalSyntax<BaseTypeListNode> ParseEnumBaseType()
    {
        if (Current.Kind != TokenKind.Colon)
            return OptionalSyntax<BaseTypeListNode>.None;

        var colon = Expect(TokenKind.Colon);
        var types = ImmutableArray.CreateBuilder<TypeSyntax>();

        types.Add(ParseExpectedType());
        var baseTypes = new SeparatedSyntaxList<TypeSyntax>(
            types.ToImmutable(),
            ImmutableArray<SyntaxToken>.Empty);

        return OptionalSyntax.With(new BaseTypeListNode(colon, baseTypes));
    }

    private ImmutableArray<EnumMemberDeclarationNode> ParseEnumMemberDeclarations()
    {
        using var _ = PushContext(ParserContext.EnumMember);

        var members = ImmutableArray.CreateBuilder<EnumMemberDeclarationNode>();

        while (!isInErrorRecovery && Current.Kind is not (TokenKind.EndOfFile or TokenKind.CloseBrace))
        {
            var attributes = ParseAttributes();

            var member = ParseEnumMemberDeclaration(attributes);

            members.Add(member);

            if (isInErrorRecovery && IsTokenValidInPrecedingContext(Current.Kind))
            {
                isInErrorRecovery = false;
                break;
            }

            if (isInErrorRecovery) Synchronize();
        }

        return members.ToImmutable();
    }

    private EnumMemberDeclarationNode ParseEnumMemberDeclaration(ImmutableArray<AttributeSectionNode> attributes)
    {
        using var _ = PushContext(ParserContext.EnumMember);

        var identifier = ExpectIdentifier();
        var assign = ExpectOptional(TokenKind.Assign);
        var expression = assign.HasValue switch
        {
            true => OptionalSyntax.With(ParseExpression()),
            false => OptionalSyntax<ExpressionNode>.None
        };
        var comma = ExpectIf(TokenKind.Comma, Lookahead.Kind != TokenKind.CloseBrace, ",");

        return new EnumMemberDeclarationNode(attributes, identifier, assign, expression, comma);
    }

    private DelegateDeclarationNode ParseDelegateDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<DelegateDeclarationNode>(modifiers);

        var enumKeyword = Expect(TokenKind.Delegate, "delegate");
        var type = ParseExpectedType();
        var identifier = ExpectIdentifier();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList(TokenKind.CloseParen);
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new DelegateDeclarationNode(attributes, modifiers, enumKeyword, type, identifier, openParen, parameters,
            closeParen, semicolon);
    }

    private ClassDeclarationNode ParseClassDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ClassDeclarationNode>(modifiers);

        var classKeyword = Expect(TokenKind.Class, "class");
        var identifier = ExpectIdentifier();
        var baseTypes = ParseBaseTypeList();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations();
        var closeBrace = Expect(TokenKind.CloseBrace, "}");
        var semicolon = ExpectOptional(TokenKind.Semicolon);

        return new ClassDeclarationNode(attributes, modifiers, classKeyword, identifier, baseTypes, openBrace,
            declarations,
            closeBrace,
            semicolon);
    }

    private OptionalSyntax<BaseTypeListNode> ParseBaseTypeList()
    {
        if (Current.Kind != TokenKind.Colon)
            return OptionalSyntax<BaseTypeListNode>.None;

        var colon = Expect(TokenKind.Colon);
        var types = ImmutableArray.CreateBuilder<TypeSyntax>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        types.Add(ParseExpectedType());

        while (Current.Kind == TokenKind.Comma)
        {
            var comma = Expect(TokenKind.Comma);
            separators.Add(comma);

            if (isInErrorRecovery)
                Synchronize();

            types.Add(ParseExpectedType());
        }

        var baseTypes = new SeparatedSyntaxList<TypeSyntax>(
            types.ToImmutable(),
            separators.ToImmutable());

        return OptionalSyntax.With(new BaseTypeListNode(colon, baseTypes));
    }

    private StructDeclarationNode ParseStructDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ClassDeclarationNode>(modifiers);

        var structKeyword = Expect(TokenKind.Struct, "struct");
        var identifier = ExpectIdentifier();
        var baseTypes = ParseBaseTypeList();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations();
        var closeBrace = Expect(TokenKind.CloseBrace, "}");
        var semicolon = ExpectOptional(TokenKind.Semicolon);

        return new StructDeclarationNode(attributes, modifiers, structKeyword, identifier, baseTypes, openBrace,
            declarations,
            closeBrace,
            semicolon);
    }

    private InterfaceDeclarationNode ParseInterfaceDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<InterfaceDeclarationNode>(modifiers);

        var interfaceKeyword = Expect(TokenKind.Interface, "interface");
        var identifier = ExpectIdentifier();
        var baseTypes = ParseBaseTypeList();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations();
        var closeBrace = Expect(TokenKind.CloseBrace, "}");
        var semicolon = ExpectOptional(TokenKind.Semicolon);

        return new InterfaceDeclarationNode(
            attributes,
            modifiers,
            interfaceKeyword,
            identifier,
            baseTypes,
            openBrace,
            declarations,
            closeBrace,
            semicolon);
    }

    private ConversionOperatorDeclarationNode ParseConversionOperatorDeclaration(
        ImmutableArray<AttributeSectionNode> attributes, ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ConversionOperatorDeclarationNode>(modifiers);

        var implicitOrExplicit = Expect(Current.Kind);
        var operatorKeyword = Expect(TokenKind.Operator, "operator");
        var conversionType = ParseExpectedType();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList(TokenKind.CloseParen);
        var closeParen = Expect(TokenKind.CloseParen, ")");

        BlockNodeOrToken blockOrSemicolon = Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(),
            _ => Expect(TokenKind.Semicolon, ";")
        };

        return new ConversionOperatorDeclarationNode(
            attributes,
            modifiers,
            conversionType,
            implicitOrExplicit,
            operatorKeyword,
            openParen,
            parameters,
            closeParen,
            blockOrSemicolon);
    }

    private OperatorDeclarationNode ParseOperatorDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers, TypeSyntax returnType)
    {
        ValidateModifiers<OperatorDeclarationNode>(modifiers);

        var operatorKeyword = Expect(TokenKind.Operator, "operator");

        var operatorToken = Current.Kind.IsOverloadableOperator() switch
        {
            true => Expect(Current.Kind),
            false => Expect(TokenKind.Unknown)
        };

        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList(TokenKind.CloseParen);
        var closeParen = Expect(TokenKind.CloseParen, ")");

        BlockNodeOrToken blockOrSemicolon = Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(),
            _ => Expect(TokenKind.Semicolon, ";")
        };

        return new OperatorDeclarationNode(
            attributes,
            modifiers,
            returnType,
            operatorKeyword,
            operatorToken,
            openParen,
            parameters,
            closeParen,
            blockOrSemicolon);
    }

    private MethodDeclarationNode ParseMethodDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers, TypeSyntax returnType,
        OptionalSyntax<ExplicitInterfaceNode> explicitInterface)
    {
        using var _ = PushContext(ParserContext.Statement);
        ValidateModifiers<MethodDeclarationNode>(modifiers);


        var identifier = ExpectIdentifier();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList(TokenKind.CloseParen);
        var closeParen = Expect(TokenKind.CloseParen, ")");

        BlockNodeOrToken blockOrSemicolon = Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(),
            _ => Expect(TokenKind.Semicolon, ";")
        };


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

    private ConstructorDeclarationNode ParseConstructorDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ConstructorDeclarationNode>(modifiers);

        var identifier = ExpectIdentifier();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var parameters = ParseParameterList(TokenKind.CloseParen);
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var initializer = ParseConstructorInitializer();

        BlockNodeOrToken blockOrSemicolon = Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(),
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
            initializer,
            blockOrSemicolon);
    }

    private OptionalSyntax<ConstructorInitializerNode> ParseConstructorInitializer()
    {
        if (Current.Kind != TokenKind.Colon)
            return OptionalSyntax<ConstructorInitializerNode>.None;

        var colon = Expect(TokenKind.Colon);

        if (Current.Kind is not (TokenKind.Base or TokenKind.This))
        {
            diagnostics.ReportError(Current.Position, "Expected 'base' or 'this'");

            isInErrorRecovery = true;

            return OptionalSyntax.With(new ConstructorInitializerNode(
                colon,
                SynthesizeToken(TokenKind.This),
                SynthesizeToken(TokenKind.OpenParen),
                SeparatedSyntaxList<ArgumentNode>.Empty,
                SynthesizeToken(TokenKind.CloseParen)));
        }

        var baseOrThis = Expect(Current.Kind);
        var openParen = Expect(TokenKind.OpenParen, "(");
        var arguments = ParseArgumentList();
        var closeParen = Expect(TokenKind.CloseParen, ")");

        return OptionalSyntax.With(new ConstructorInitializerNode(
            colon,
            baseOrThis,
            openParen,
            arguments,
            closeParen));
    }

    private DestructorDeclarationNode ParseDestructorDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<DestructorDeclarationNode>(modifiers);

        var tilde = Expect(TokenKind.Tilde, "~");
        var identifier = ExpectIdentifier();
        var openParen = Expect(TokenKind.OpenParen, "(");
        var closeParen = Expect(TokenKind.CloseParen, ")");

        BlockNodeOrToken blockOrSemicolon = Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(),
            _ => Expect(TokenKind.Semicolon, ";")
        };

        if (isInErrorRecovery) isInErrorRecovery = false;

        return new DestructorDeclarationNode(
            attributes,
            modifiers,
            tilde,
            identifier,
            openParen,
            closeParen,
            blockOrSemicolon);
    }

    private SeparatedSyntaxList<ArgumentNode> ParseArgumentList()
    {
        var arguments = ImmutableArray.CreateBuilder<ArgumentNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind != TokenKind.CloseParen && Current.Kind != TokenKind.EndOfFile)
        {
            if (arguments.Count > 0)
            {
                if (Current.Kind != TokenKind.Comma)
                    break;

                separators.Add(Expect(TokenKind.Comma, ","));

                if (isInErrorRecovery)
                    Synchronize();
            }

            var argument = ParseArgument();
            arguments.Add(argument);
        }

        return new SeparatedSyntaxList<ArgumentNode>(arguments.ToImmutable(), separators.ToImmutable());
    }

    private ArgumentNode ParseArgument()
    {
        var refOrOut = Current.Kind switch
        {
            TokenKind.Ref or TokenKind.Out =>
                OptionalSyntax.With(Expect(Current.Kind)),
            _ => OptionalSyntax<SyntaxToken>.None
        };

        var expression = ParseExpression();

        return new ArgumentNode(refOrOut, expression);
    }

    private MemberDeclarationNode ParseEventDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        using var _ = PushContext(ParserContext.Event);

        ValidateModifiers<EventDeclarationNode>(modifiers);

        var eventKeyword = Expect(TokenKind.Event, "event");
        var type = ParseExpectedType();

        if (Lookahead.Kind is not (TokenKind.Dot or TokenKind.OpenBrace))
            return ParseEventFieldDeclaration(attributes, modifiers, eventKeyword, type);

        var explicitInterface = Lookahead.Kind switch
        {
            TokenKind.Dot => ParseExplicitInterface(),
            _ => OptionalSyntax<ExplicitInterfaceNode>.None
        };
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var accessors = ParseAccessorDeclarations();
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new EventDeclarationNode(attributes, modifiers, eventKeyword, type, explicitInterface, identifier,
            openBrace, accessors, closeBrace);
    }

    private MemberDeclarationNode ParseEventFieldDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers, SyntaxToken eventKeyword, TypeSyntax type)
    {
        var declarators = ParseVariableDeclarators();
        var semicolon = Expect(TokenKind.Semicolon);

        return new EventFieldDeclarationNode(attributes, modifiers, eventKeyword, type, declarators, semicolon);
    }


    private FieldDeclarationNode ParseConstantFieldDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        using var _ = PushContext(ParserContext.Constant);
        ValidateModifiers<FieldDeclarationNode>(modifiers);

        var constKeyword = OptionalSyntax.With(Expect(TokenKind.Const, "const"));
        var type = ParseExpectedType();
        var declarators = ParseVariableDeclarators();
        var semicolon = Expect(TokenKind.Semicolon);

        return new FieldDeclarationNode(
            attributes,
            modifiers,
            constKeyword,
            type,
            declarators,
            semicolon);
    }

    private FieldDeclarationNode ParseFieldDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        TypeSyntax type)
    {
        ValidateModifiers<FieldDeclarationNode>(modifiers);

        var declarators = ParseVariableDeclarators();

        var semicolon = Expect(TokenKind.Semicolon);

        return new FieldDeclarationNode(
            attributes,
            modifiers,
            OptionalSyntax<SyntaxToken>.None,
            type,
            declarators,
            semicolon);
    }

    private SeparatedSyntaxList<VariableDeclaratorNode> ParseVariableDeclarators()
    {
        var declarators = ImmutableArray.CreateBuilder<VariableDeclaratorNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        declarators.Add(ParseVariableDeclarator());

        while (Current.Kind == TokenKind.Comma)
        {
            var comma = Expect(TokenKind.Comma);

            declarators.Add(ParseVariableDeclarator());
            separators.Add(comma);

            if (isInErrorRecovery && IsTokenValidInPrecedingContext(Current.Kind))
            {
                isInErrorRecovery = false;
                break;
            }

            if (isInErrorRecovery) Synchronize();
        }

        return new SeparatedSyntaxList<VariableDeclaratorNode>(declarators.ToImmutable(), separators.ToImmutable());
    }

    private VariableDeclaratorNode ParseVariableDeclarator()
    {
        var identifier = ExpectIdentifier();

        var assign = ExpectOptional(TokenKind.Assign);

        var initializer = assign.HasValue switch
        {
            true => OptionalSyntax.With(Current.Kind switch
            {
                TokenKind.OpenBrace => ParseArrayInitializer(),
                _ => ParseExpression()
            }),
            false => OptionalSyntax<ExpressionNode>.None
        };
        return new VariableDeclaratorNode(identifier, assign, initializer);
    }

    private SeparatedSyntaxList<ParameterNode> ParseParameterList(TokenKind closeToken)
    {
        using var _ = PushContext(ParserContext.ParameterList);

        var parameters = ImmutableArray.CreateBuilder<ParameterNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind != closeToken && Current.Kind != TokenKind.EndOfFile)
        {
            if (parameters.Count > 0)
            {
                separators.Add(Expect(TokenKind.Comma, ","));

                if (isInErrorRecovery && IsTokenValidInPrecedingContext(Current.Kind))
                {
                    isInErrorRecovery = false;
                    break;
                }

                if (isInErrorRecovery) Synchronize();

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
        var attributes = ParseAttributes();
        var modifiers = ParseParameterModifiers();
        var type = ParseExpectedType();

        var identifier = ExpectIdentifier();

        return new ParameterNode(attributes, modifiers, type, identifier);
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

    private PropertyDeclarationNode ParsePropertyDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        TypeSyntax type, OptionalSyntax<ExplicitInterfaceNode> explicitInterface)
    {
        using var _ = PushContext(ParserContext.Property);

        ValidateModifiers<PropertyDeclarationNode>(modifiers);

        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var accessors = ParseAccessorDeclarations();
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

    private ImmutableArray<AccessorDeclarationNode> ParseAccessorDeclarations()
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
                "add" => Current with { Kind = TokenKind.Add },
                "remove" => Current with { Kind = TokenKind.Remove },
                _ => Current
            });

            var keywordKind = keyword.Element!.Kind;

            var isValidForContext = keywordKind switch
            {
                TokenKind.Get or TokenKind.Set => currentContext is ParserContext.Property or ParserContext.Indexer,
                TokenKind.Add or TokenKind.Remove => currentContext == ParserContext.Event,
                _ => false
            };

            if (!isValidForContext)
            {
                diagnostics.ReportError(keyword.Element!.Position,
                    currentContext == ParserContext.Property
                        ? "A get or set accessor expected"
                        : "A add or remove accessor expected");

                if (keywordKind is not (TokenKind.OpenBrace or TokenKind.Semicolon))
                {
                    if (IsTokenValidInPrecedingContext(Current.Kind))
                    {
                        accessors.Add(HandleIncompleteAccessor(accessorAttributes, accessorModifiers));
                        isInErrorRecovery = false;

                        break;
                    }

                    isInErrorRecovery = true;

                    Synchronize();

                    continue;
                }

                keyword = OptionalSyntax<SyntaxToken>.None;
            }

            accessors.Add(ParseAccessorDeclaration(accessorAttributes, accessorModifiers, keyword));

            if (isInErrorRecovery)
                Synchronize();
        }

        return accessors.ToImmutable();
    }

    private IndexerDeclarationNode ParseIndexerDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        TypeSyntax type, OptionalSyntax<ExplicitInterfaceNode> explicitInterface)
    {
        using var _ = PushContext(ParserContext.Indexer);

        ValidateModifiers<IndexerDeclarationNode>(modifiers);

        var thisKeyword = Expect(TokenKind.This, "this");
        var openBracket = Expect(TokenKind.OpenBracket, "[");
        var parameters = ParseParameterList(TokenKind.CloseBracket);
        var closeBracket = Expect(TokenKind.CloseBracket, "]");
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var accessors = ParseAccessorDeclarations();
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

    private SimpleNameNode ParseSimpleName()
    {
        var identifier = ExpectIdentifier();

        return new SimpleNameNode(identifier);
    }

    private MemberNameNode ParseQualifiedName()
    {
        MemberNameNode left = ParseSimpleName();

        while (Current.Kind == TokenKind.Dot)
        {
            var dot = Expect(TokenKind.Dot);

            var right = ParseSimpleName();

            left = new QualifiedNameNode(left, dot, right);
        }

        return left;
    }

    private MemberNameNode ParseMemberName()
    {
        MemberNameNode left = ParseSimpleName();

        while (Current.Kind == TokenKind.Dot)
        {
            if (tokenStream.Peek(2).Kind is not (TokenKind.Identifier or TokenKind.Dot))
                break;

            var dot = Expect(TokenKind.Dot);

            var right = ParseSimpleName();

            left = new QualifiedNameNode(left, dot, right);
        }

        return left;
    }

    private OptionalSyntax<ExplicitInterfaceNode> ParseExplicitInterface()
    {
        if (Lookahead.Kind != TokenKind.Dot)
            return OptionalSyntax<ExplicitInterfaceNode>.None;

        var name = ParseMemberName();
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
            body = OptionalSyntax.With(ParseBlockStatement());
        else
            semicolon = OptionalSyntax.With(Expect(TokenKind.Semicolon, ";"));

        return new AccessorDeclarationNode(attributes, modifiers, keyword, body, semicolon);
    }

    private StatementNode ParseStatement()
    {
        using var _ = PushContext(ParserContext.Statement);

        if (Current.Kind == TokenKind.Identifier && Lookahead.Kind == TokenKind.Colon)
            return ParseLabeledStatement();

        switch (Current.Kind)
        {
            case TokenKind.OpenBrace:
                return ParseBlockStatement();
            case TokenKind.If:
                return ParseIfStatement();
            case TokenKind.Switch:
                return ParseSwitchStatement();
            case TokenKind.For:
                return ParseForStatement();
            case TokenKind.Foreach:
                return ParseForeachStatement();
            case TokenKind.While:
                return ParseWhileStatement();
            case TokenKind.Do:
                return ParseDoStatement();
            case TokenKind.Break:
                return ParseBreakStatement();
            case TokenKind.Continue:
                return ParseContinueStatement();
            case TokenKind.Goto when Lookahead.Kind == TokenKind.Default:
                return ParseGotoDefaultStatement();
            case TokenKind.Goto when Lookahead.Kind == TokenKind.Case:
                return ParseGotoCaseStatement();
            case TokenKind.Goto:
                return ParseGotoStatement();
            case TokenKind.Return:
                return ParseReturnStatement();
            case TokenKind.Throw:
                return ParseThrowStatement();
            case TokenKind.Checked when Lookahead.Kind != TokenKind.OpenParen:
                return ParseCheckedStatement();
            case TokenKind.Unchecked when Lookahead.Kind != TokenKind.OpenParen:
                return ParseUncheckedStatement();
            case TokenKind.Lock:
                return ParseLockStatement();
            case TokenKind.Using:
                return ParseUsingStatement();
            case TokenKind.Try:
                return ParseTryStatement();
            case TokenKind.Unsafe:
                return ParseUnsafeStatement();
            case TokenKind.Fixed:
                return ParseFixedStatement();

            case TokenKind.Semicolon:
                return new EmptyStatementNode(Expect(TokenKind.Semicolon, ";"));

            case TokenKind.Const:
            case TokenKind.Identifier:
            case var kind when kind.IsPredefinedType():
                var constKeyword = ExpectOptional(TokenKind.Const);

                var restorePoint = tokenStream.CreateRestorePoint();
                var suppress = diagnostics.Suppress();

                var type = ParseType();

                if (constKeyword.HasValue || (type != null && Current.Kind == TokenKind.Identifier))
                    return ParseDeclarationStatement(constKeyword, type);

                tokenStream.Restore(restorePoint);
                suppress.Restore();


                goto default;
            default:
                return ParseExpressionStatement();
        }
    }

    private BreakStatementNode ParseBreakStatement()
    {
        var breakKeyword = Expect(TokenKind.Break, "break");
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new BreakStatementNode(breakKeyword, semicolon);
    }

    private ContinueStatementNode ParseContinueStatement()
    {
        var continueKeyword = Expect(TokenKind.Continue, "continue");
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new ContinueStatementNode(continueKeyword, semicolon);
    }

    private GotoStatementNode ParseGotoStatement()
    {
        var gotoKeyword = Expect(TokenKind.Goto, "goto");
        var identifier = ExpectIdentifier();
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new GotoStatementNode(gotoKeyword, identifier, semicolon);
    }

    private GotoDefaultStatementNode ParseGotoDefaultStatement()
    {
        var gotoKeyword = Expect(TokenKind.Goto, "goto");
        var defaultKeyword = Expect(TokenKind.Default, "default");
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new GotoDefaultStatementNode(gotoKeyword, defaultKeyword, semicolon);
    }

    private GotoCaseStatementNode ParseGotoCaseStatement()
    {
        var gotoKeyword = Expect(TokenKind.Goto, "goto");
        var caseKeyword = Expect(TokenKind.Case, "case");
        var expression = ParseExpression();
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new GotoCaseStatementNode(gotoKeyword, caseKeyword, expression, semicolon);
    }

    private ReturnStatementNode ParseReturnStatement()
    {
        var returnKeyword = Expect(TokenKind.Return, "return");
        var expression = Current.Kind switch
        {
            TokenKind.Semicolon => OptionalSyntax<ExpressionNode>.None,
            _ => ParseExpression()
        };
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new ReturnStatementNode(returnKeyword, expression, semicolon);
    }

    private ThrowStatementNode ParseThrowStatement()
    {
        var throwKeyword = Expect(TokenKind.Throw, "throw");
        var expression = Current.Kind switch
        {
            TokenKind.Semicolon => OptionalSyntax<ExpressionNode>.None,
            _ => ParseExpression()
        };
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new ThrowStatementNode(throwKeyword, expression, semicolon);
    }

    private CheckedStatementNode ParseCheckedStatement()
    {
        var checkedKeyword = Expect(TokenKind.Checked, "checked");
        var block = ParseBlockStatement();

        return new CheckedStatementNode(checkedKeyword, block);
    }

    private UncheckedStatementNode ParseUncheckedStatement()
    {
        var uncheckedKeyword = Expect(TokenKind.Unchecked, "unchecked");
        var block = ParseBlockStatement();

        return new UncheckedStatementNode(uncheckedKeyword, block);
    }

    private LockStatementNode ParseLockStatement()
    {
        var lockKeyword = Expect(TokenKind.Lock, "lock");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var expression = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var statement = ParseStatement();

        return new LockStatementNode(lockKeyword, openParen, expression, closeParen, statement);
    }

    private UsingStatementNode ParseUsingStatement()
    {
        var usingKeyword = Expect(TokenKind.Using);
        var openParen = Expect(TokenKind.OpenParen, "(");

        var restorePoint = tokenStream.CreateRestorePoint();
        var suppress = diagnostics.Suppress();

        var type = ParseType();

        SyntaxUnion<VariableDeclarationNode, ExpressionNode> declaration;

        if (type != null && Current.Kind == TokenKind.Identifier)
        {
            declaration =
                new VariableDeclarationNode(OptionalSyntax<SyntaxToken>.None, type, ParseVariableDeclarators());
        }
        else
        {
            tokenStream.Restore(restorePoint);
            suppress.Restore();

            declaration = ParseExpression();
        }

        var closeParen = Expect(TokenKind.CloseParen, ")");
        var statement = ParseStatement();

        return new UsingStatementNode(
            usingKeyword,
            openParen,
            declaration,
            closeParen,
            statement);
    }

    private TryStatementNode ParseTryStatement()
    {
        var tryKeyword = Expect(TokenKind.Try);
        var block = ParseBlockStatement();
        var catchClauses = ImmutableArray.CreateBuilder<CatchClauseNode>();

        while (Current.Kind == TokenKind.Catch)
            catchClauses.Add(ParseCatchClause());

        var finallyClause = Current.Kind switch
        {
            TokenKind.Finally => OptionalSyntax.With(ParseFinallyClause()),
            _ => OptionalSyntax<FinallyClauseNode>.None
        };

        return new TryStatementNode(
            tryKeyword,
            block,
            catchClauses.ToImmutable(),
            finallyClause);
    }

    private CatchClauseNode ParseCatchClause()
    {
        var catchKeyword = Expect(TokenKind.Catch);

        OptionalSyntax<SyntaxToken> openParen;
        OptionalSyntax<TypeSyntax> type;
        OptionalSyntax<SyntaxToken> identifier;
        OptionalSyntax<SyntaxToken> closeParen;

        if (Current.Kind == TokenKind.OpenParen)
        {
            openParen = Expect(TokenKind.OpenParen);
            type = ParseExpectedType();

            identifier = Current.Kind switch
            {
                TokenKind.Identifier => OptionalSyntax.With(ExpectIdentifier()),
                _ => OptionalSyntax<SyntaxToken>.None
            };

            closeParen = Expect(TokenKind.CloseParen);
        }
        else
        {
            openParen = OptionalSyntax<SyntaxToken>.None;
            type = OptionalSyntax<TypeSyntax>.None;
            identifier = OptionalSyntax<SyntaxToken>.None;
            closeParen = OptionalSyntax<SyntaxToken>.None;
        }

        var block = ParseBlockStatement();

        return new CatchClauseNode(catchKeyword, openParen, type, identifier, closeParen, block);
    }

    private FinallyClauseNode ParseFinallyClause()
    {
        var finallyKeyword = Expect(TokenKind.Finally);
        var block = ParseBlockStatement();

        return new FinallyClauseNode(finallyKeyword, block);
    }

    private UnsafeStatementNode ParseUnsafeStatement()
    {
        var unsafeKeyword = Expect(TokenKind.Unsafe);
        var block = ParseBlockStatement();

        return new UnsafeStatementNode(unsafeKeyword, block);
    }

    private FixedStatementNode ParseFixedStatement()
    {
        var fixedKeyword = Expect(TokenKind.Fixed);
        var openParen = Expect(TokenKind.OpenParen);
        var type = ParseExpectedType();
        var declaration =
            new VariableDeclarationNode(OptionalSyntax<SyntaxToken>.None, type, ParseVariableDeclarators());
        var closeParen = Expect(TokenKind.CloseParen);
        var statement = ParseStatement();

        return new FixedStatementNode(fixedKeyword, openParen, declaration, closeParen, statement);
    }

    private LabeledStatementNode ParseLabeledStatement()
    {
        var identifier = ExpectIdentifier();
        var colon = Expect(TokenKind.Colon, ":");
        var statement = ParseStatement();

        return new LabeledStatementNode(identifier, colon, statement);
    }

    private DeclarationStatementNode ParseDeclarationStatement(OptionalSyntax<SyntaxToken> constKeyword,
        TypeSyntax? type)
    {
        if (type == null)
        {
            diagnostics.ReportError(Current.Position, "Type expected");

            isInErrorRecovery = true;
        }

        var declarators = ParseVariableDeclarators();
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new DeclarationStatementNode(
            new VariableDeclarationNode(constKeyword, OptionalSyntax.With(type), declarators), semicolon);
    }

    private BlockStatementNode ParseBlockStatement()
    {
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var statements = ImmutableArray.CreateBuilder<StatementNode>();

        while (Current.Kind != TokenKind.CloseBrace && Current.Kind != TokenKind.EndOfFile)
        {
            var statement = ParseStatement();
            statements.Add(statement);

            if (isInErrorRecovery)
            {
                Synchronize();

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

    private OptionalSyntax<ElseClauseNode> ParseElseClause()
    {
        if (Current.Kind != TokenKind.Else)
            return OptionalSyntax<ElseClauseNode>.None;

        var elseKeyword = Expect(TokenKind.Else, "else");
        var statement = ParseStatement();

        return OptionalSyntax.With(new ElseClauseNode(elseKeyword, statement));
    }

    private IfStatementNode ParseIfStatement()
    {
        var ifKeyword = Expect(TokenKind.If, "if");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var condition = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var statement = ParseStatement();
        var elseClause = ParseElseClause();

        return new IfStatementNode(ifKeyword, openParen, condition, closeParen, statement, elseClause);
    }

    private SwitchStatementNode ParseSwitchStatement()
    {
        var switchKeyword = Expect(TokenKind.Switch);
        var openParen = Expect(TokenKind.OpenParen, "(");
        var expression = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var openBrace = Expect(TokenKind.OpenBrace, "{");

        var sections = ImmutableArray.CreateBuilder<SwitchSectionNode>();

        while (Current.Kind is TokenKind.Case or TokenKind.Default) sections.Add(ParseSwitchSection());

        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new SwitchStatementNode(
            switchKeyword,
            openParen,
            expression,
            closeParen,
            openBrace,
            sections.ToImmutable(),
            closeBrace);
    }

    private SwitchSectionNode ParseSwitchSection()
    {
        var labels = ImmutableArray.CreateBuilder<SwitchLabelNode>();
        var statements = ImmutableArray.CreateBuilder<StatementNode>();

        do
        {
            labels.Add(ParseSwitchLabel());
        } while (Current.Kind is TokenKind.Case or TokenKind.Default);

        while (Current.Kind is not (TokenKind.Case or TokenKind.Default or TokenKind.CloseBrace))
        {
            var statement = ParseStatement();
            statements.Add(statement);

            if (isInErrorRecovery)
                Synchronize();
        }

        return new SwitchSectionNode(labels.ToImmutable(), statements.ToImmutable());
    }

    private SwitchLabelNode ParseSwitchLabel()
    {
        var caseOrDefault = Expect(Current.Kind);

        var expression = caseOrDefault.Kind switch
        {
            TokenKind.Case => OptionalSyntax.With(ParseExpression()),
            _ => OptionalSyntax<ExpressionNode>.None
        };

        var colon = Expect(TokenKind.Colon, ":");

        return new SwitchLabelNode(caseOrDefault, expression, colon);
    }

    private ForStatementNode ParseForStatement()
    {
        var forKeyword = Expect(TokenKind.For, "for");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var initializers = ParseForInitializers();
        var firstSemicolon = Expect(TokenKind.Semicolon, ";");
        var condition = Current.Kind != TokenKind.Semicolon
            ? OptionalSyntax.With(ParseExpression())
            : OptionalSyntax<ExpressionNode>.None;
        var secondSemicolon = Expect(TokenKind.Semicolon, ";");
        var iterator = Current.Kind != TokenKind.CloseParen
            ? ParseStatementExpressionList()
            : SeparatedSyntaxList<ExpressionNode>.Empty;
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var statement = ParseStatement();

        return new ForStatementNode(forKeyword, openParen, initializers, firstSemicolon, condition, secondSemicolon,
            iterator, closeParen, statement);
    }

    private VariableDeclarationOrInitializer ParseForInitializers()
    {
        if (Current.Kind == TokenKind.Semicolon)
            return SeparatedSyntaxList<ExpressionNode>.Empty;

        switch (Current.Kind)
        {
            case TokenKind.Identifier:
            case var kind when kind.IsPredefinedType():
                break;
        }

        var restorePoint = tokenStream.CreateRestorePoint();
        var suppress = diagnostics.Suppress();

        var type = ParseType();

        if (type != null && Current.Kind == TokenKind.Identifier)
            return new VariableDeclarationNode(OptionalSyntax<SyntaxToken>.None, type, ParseVariableDeclarators());

        tokenStream.Restore(restorePoint);
        suppress.Restore();

        return ParseStatementExpressionList();
    }

    private SeparatedSyntaxList<ExpressionNode> ParseStatementExpressionList()
    {
        var expressions = ImmutableArray.CreateBuilder<ExpressionNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        expressions.Add(ParseExpression());

        while (Current.Kind == TokenKind.Comma)
        {
            separators.Add(Expect(TokenKind.Comma));

            if (isInErrorRecovery)
                Synchronize();

            expressions.Add(ParseExpression());
        }

        return new SeparatedSyntaxList<ExpressionNode>(
            expressions.ToImmutable(),
            separators.ToImmutable());
    }

    private ForeachStatementNode ParseForeachStatement()
    {
        var foreachKeyword = Expect(TokenKind.Foreach, "foreach");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var type = ParseExpectedType();
        var identifier = ExpectIdentifier();
        var inKeyword = Expect(TokenKind.In, "in");
        var expression = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var statement = ParseStatement();

        return new ForeachStatementNode(foreachKeyword, openParen, type, identifier, inKeyword, expression, closeParen,
            statement);
    }

    private WhileStatementNode ParseWhileStatement()
    {
        var whileKeyword = Expect(TokenKind.While, "while");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var condition = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var statement = ParseStatement();

        return new WhileStatementNode(whileKeyword, openParen, condition, closeParen, statement);
    }

    private DoStatementNode ParseDoStatement()
    {
        var doKeyword = Expect(TokenKind.Do, "do");
        var statement = ParseStatement();
        var whileKeyword = Expect(TokenKind.While, "while");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var condition = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");
        var semicolon = Expect(TokenKind.Semicolon, ";");

        return new DoStatementNode(doKeyword, statement, whileKeyword, openParen, condition, closeParen, semicolon);
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
        var condition = ParseConditionalOrExpression();

        if (Current.Kind != TokenKind.Question)
            return condition;

        var questionToken = Expect(TokenKind.Question);
        var ifTrue = ParseExpression();
        var colonToken = Expect(TokenKind.Colon, ":");
        var ifFalse = ParseExpression();

        return new ConditionalExpressionNode(condition, questionToken, ifTrue, colonToken, ifFalse);
    }

    private ExpressionNode ParseConditionalOrExpression()
    {
        var left = ParseConditionalAndExpression();

        while (Current.Kind == TokenKind.OrOr)
        {
            var operatorToken = Expect(TokenKind.OrOr);
            var right = ParseConditionalAndExpression();
            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionNode ParseConditionalAndExpression()
    {
        var left = ParseLogicalOrExpression();

        while (Current.Kind == TokenKind.AndAnd)
        {
            var operatorToken = Expect(TokenKind.AndAnd);
            var right = ParseLogicalOrExpression();
            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionNode ParseLogicalOrExpression()
    {
        var left = ParseLogicalAndExpression();

        while (Current.Kind == TokenKind.Pipe)
        {
            var operatorToken = Expect(Current.Kind);
            var right = ParseLogicalAndExpression();

            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionNode ParseLogicalAndExpression()
    {
        var left = ParseLogicalXorExpression();

        while (Current.Kind == TokenKind.Ampersand)
        {
            var operatorToken = Expect(Current.Kind);
            var right = ParseLogicalXorExpression();

            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionNode ParseLogicalXorExpression()
    {
        var left = ParseEqualityExpression();

        while (Current.Kind == TokenKind.Xor)
        {
            var operatorToken = Expect(Current.Kind);
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
        var left = ParseShiftExpression();

        while (true)
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
                    var right = ParseShiftExpression();
                    left = new BinaryExpressionNode(left, operatorToken, right);
                    break;
            }
    }

    private ExpressionNode ParseShiftExpression()
    {
        var left = ParseAdditiveExpression();

        while (Current.Kind.IsShiftOperator())
        {
            var operatorToken = Expect(Current.Kind);
            var right = ParseAdditiveExpression();

            left = new BinaryExpressionNode(left, operatorToken, right);
        }

        return left;
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
        if (Current.Kind == TokenKind.OpenParen && IsPossibleCastExpression())
            return ParseCastExpression();

        if (!Current.Kind.IsUnaryOperator())
            return ParsePrimaryExpression();

        var operatorToken = Expect(Current.Kind);
        var operand = ParseUnaryExpression();

        return new PrefixUnaryExpressionNode(operatorToken, operand);
    }

    private CastExpressionNode ParseCastExpression()
    {
        var openParen = Expect(TokenKind.OpenParen);
        var type = ParseExpectedType();
        var closeParen = Expect(TokenKind.CloseParen);
        var expression = ParseUnaryExpression();

        return new CastExpressionNode(openParen, type, closeParen, expression);
    }

    private ExpressionNode ParsePrimaryExpression()
    {
        var expression = Current.Kind switch
        {
            TokenKind.False or TokenKind.True => new LiteralExpressionNode(Expect(Current.Kind)),
            TokenKind.Null => new LiteralExpressionNode(Expect(TokenKind.Null)),
            TokenKind.NumericLiteral
                or TokenKind.StringLiteral
                or TokenKind.CharacterLiteral => new LiteralExpressionNode(Expect(Current.Kind)),
            TokenKind.This => new ThisExpressionNode(Expect(TokenKind.This)),
            TokenKind.Base => new BaseExpressionNode(Expect(TokenKind.Base)),
            TokenKind.New => ParseNewExpression(),
            TokenKind.StackAlloc => ParseStackAllocExpression(),
            TokenKind.SizeOf => ParseSizeOfExpression(),
            TokenKind.TypeOf => ParseTypeOfExpression(),
            TokenKind.Checked => ParseCheckedExpression(),
            TokenKind.Unchecked => ParseUncheckedExpression(),
            TokenKind.OpenParen => ParseParenthesizedExpression(),
            TokenKind.Identifier => new IdentifierExpressionNode(ExpectIdentifier()),
            var kind when kind.IsPredefinedType() => new PredefinedTypeExpression(ParsePredefinedType()!),
            _ => null
        };

        if (isInErrorRecovery)
            return expression!;

        if (expression == null)
        {
            var current = Current;
            diagnostics.ReportError(Current.Position, "Expected expression");
            isInErrorRecovery = true;
            tokenStream.Advance();
            expression = new ErrorExpressionNode(current);
        }

        while (true)
            switch (Current.Kind)
            {
                case TokenKind.OpenParen:
                    expression = ParseInvocationExpression(expression);
                    break;
                case TokenKind.OpenBracket:
                    expression = ParseElementAccessExpression(expression);
                    break;
                case TokenKind.Dot:
                    expression = ParseMemberAccessExpression(expression);
                    break;
                case TokenKind.Arrow:
                    expression = ParsePointerMemberAccessExpression(expression);
                    break;

                case TokenKind.MinusMinus:
                case TokenKind.PlusPlus:
                    var operatorToken = Expect(Current.Kind);
                    expression = new PostfixUnaryExpressionNode(expression, operatorToken);
                    break;

                default:
                    return expression;
            }
    }

    private ExpressionNode ParseNewExpression()
    {
        var newKeyword = Expect(TokenKind.New);
        var type = ParseNonArrayType();

        if (type != null)
            return Current.Kind switch
            {
                TokenKind.OpenBracket => ParseArrayCreationExpression(newKeyword, type),
                TokenKind.OpenParen => ParseObjectCreationExpression(newKeyword, type),
                _ => new ErrorExpressionNode(Current)
            };

        diagnostics.ReportError(Current.Position, "Type expected");

        isInErrorRecovery = true;

        return new ErrorExpressionNode(newKeyword);
    }

    private ExpressionNode ParseArrayCreationExpression(SyntaxToken newKeyword, TypeSyntax type)
    {
        var rankSpecifiers = ImmutableArray.CreateBuilder<ArrayRankSpecifierNode>();

        rankSpecifiers.Add(ParseArrayRankSpecifier());

        while (Current.Kind == TokenKind.OpenBracket)
            rankSpecifiers.Add(ParseArrayRankSpecifier());

        var initializer = Current.Kind switch
        {
            TokenKind.OpenBrace => OptionalSyntax.With(ParseArrayInitializer()),
            _ => OptionalSyntax<ArrayInitializerExpressionNode>.None
        };

        return new ArrayCreationExpressionNode(
            newKeyword,
            type,
            rankSpecifiers.ToImmutable(),
            initializer);
    }

    private ArrayInitializerExpressionNode ParseArrayInitializer()
    {
        var openBrace = Expect(TokenKind.OpenBrace);

        var expressions = ImmutableArray.CreateBuilder<ExpressionNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        if (Current.Kind != TokenKind.CloseBrace)
            do
            {
                expressions.Add(Current.Kind switch
                {
                    TokenKind.OpenBrace => ParseArrayInitializer(),
                    _ => ParseExpression()
                });

                if (Current.Kind == TokenKind.Comma)
                    separators.Add(Expect(TokenKind.Comma));
                else
                    break;

                if (Current.Kind == TokenKind.CloseBrace)
                    break;

            } while (!isInErrorRecovery);

        var closeBrace = Expect(TokenKind.CloseBrace);

        return new ArrayInitializerExpressionNode(
            openBrace,
            new SeparatedSyntaxList<ExpressionNode>(expressions.ToImmutable(), separators.ToImmutable()),
            closeBrace);
    }

    private ArrayRankSpecifierNode ParseArrayRankSpecifier()
    {
        var openBracket = Expect(TokenKind.OpenBracket);

        var sizes = ImmutableArray.CreateBuilder<ExpressionNode>();
        var separators = ImmutableArray.CreateBuilder<SyntaxToken>();

        sizes.Add(Current.Kind switch
        {
            TokenKind.Comma or TokenKind.CloseBracket => new EmptyExpressionNode(),
            _ => ParseExpression()
        });

        while (Current.Kind == TokenKind.Comma)
        {
            separators.Add(Expect(TokenKind.Comma));

            sizes.Add(Current.Kind switch
            {
                TokenKind.Comma or TokenKind.CloseBracket => new EmptyExpressionNode(),
                _ => ParseExpression()
            });
        }

        var closeBracket = Expect(TokenKind.CloseBracket);

        return new ArrayRankSpecifierNode(
            openBracket,
            new SeparatedSyntaxList<ExpressionNode>(sizes.ToImmutable(), separators.ToImmutable()),
            closeBracket);
    }

    private ObjectCreationExpressionNode ParseObjectCreationExpression(SyntaxToken newKeyword, TypeSyntax type)
    {
        var openParen = Expect(TokenKind.OpenParen);
        var arguments = ParseArgumentList();
        var closeParen = Expect(TokenKind.CloseParen);

        return new ObjectCreationExpressionNode(
            newKeyword,
            type,
            openParen,
            arguments,
            closeParen);
    }

    private bool IsPossibleCastExpression()
    {
        var restorePoint = tokenStream.CreateRestorePoint();
        var suppress = diagnostics.Suppress();

        try
        {
            tokenStream.Advance();

            var type = ParseType();

            if (type == null)
                return false;

            if (Current.Kind != TokenKind.CloseParen)
                return false;

            tokenStream.Advance();

            // The array type can look like an element access
            if (type is ArrayTypeSyntax arrayTypeSyntax)
                return arrayTypeSyntax.IsValidType;

            // If type is valid expression grammar or not
            if (type is not (SimpleTypeSyntax or QualifiedTypeSyntax))
                return true;

            return Current.Kind switch
            {
                TokenKind.Tilde => true,
                TokenKind.Exclamation => true,
                TokenKind.OpenParen => true,
                TokenKind.Identifier => true,
                _ when Current.Kind.IsLiteral() => true,
                _ when Current.Kind.IsKeyword() &&
                       Current.Kind is not (TokenKind.As or TokenKind.Is) => true,

                _ => false
            };
        }
        finally
        {
            tokenStream.Restore(restorePoint);
            suppress.Restore();
        }
    }

    private InvocationExpressionNode ParseInvocationExpression(ExpressionNode expression)
    {
        var openParen = Expect(TokenKind.OpenParen);
        var arguments = ParseArgumentList();
        var closeParen = Expect(TokenKind.CloseParen);

        return new InvocationExpressionNode(
            expression,
            openParen,
            arguments,
            closeParen);
    }

    private MemberAccessExpressionNode ParseMemberAccessExpression(ExpressionNode expression)
    {
        var dot = Expect(TokenKind.Dot);
        var name = ExpectIdentifier();

        return new MemberAccessExpressionNode(expression, dot, name);
    }

    private PointerMemberAccessExpressionNode ParsePointerMemberAccessExpression(ExpressionNode expression)
    {
        var arrow = Expect(TokenKind.Arrow);
        var name = ExpectIdentifier();

        return new PointerMemberAccessExpressionNode(expression, arrow, name);
    }

    private ElementAccessExpressionNode ParseElementAccessExpression(ExpressionNode expression)
    {
        var openBracket = Expect(TokenKind.OpenBracket);
        var arguments = ParseArgumentList();
        var closeBracket = Expect(TokenKind.CloseBracket, "]");
        return new ElementAccessExpressionNode(
            expression,
            openBracket,
            arguments,
            closeBracket);
    }

    private ParenthesizedExpressionNode ParseParenthesizedExpression()
    {
        var openParen = Expect(TokenKind.OpenParen, "(");
        var expression = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");

        return new ParenthesizedExpressionNode(openParen, expression, closeParen);
    }

    private StackAllocExpressionNode ParseStackAllocExpression()
    {
        var stackallocKeyword = Expect(TokenKind.StackAlloc);
        var elementType = ParseNonArrayType();

        var openBracket = Expect(TokenKind.OpenBracket);
        var size = ParseExpression();
        var closeBracket = Expect(TokenKind.CloseBracket);

        return new StackAllocExpressionNode(
            stackallocKeyword,
            elementType!,
            openBracket,
            size,
            closeBracket);
    }

    private SizeOfExpressionNode ParseSizeOfExpression()
    {
        var sizeOfKeyword = Expect(TokenKind.SizeOf, "sizeof");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var type = ParseExpectedType();
        var closeParen = Expect(TokenKind.CloseParen, ")");

        return new SizeOfExpressionNode(sizeOfKeyword, openParen, type, closeParen);
    }

    private TypeOfExpressionNode ParseTypeOfExpression()
    {
        var typeOfKeyword = Expect(TokenKind.TypeOf, "typeof");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var type = ParseExpectedType();
        var closeParen = Expect(TokenKind.CloseParen, ")");

        return new TypeOfExpressionNode(typeOfKeyword, openParen, type, closeParen);
    }

    private CheckedExpressionNode ParseCheckedExpression()
    {
        var checkedKeyword = Expect(TokenKind.Checked, "checked");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var expression = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");

        return new CheckedExpressionNode(checkedKeyword, openParen, expression, closeParen);
    }

    private UncheckedExpressionNode ParseUncheckedExpression()
    {
        var uncheckedKeyword = Expect(TokenKind.Unchecked, "unchecked");
        var openParen = Expect(TokenKind.OpenParen, "(");
        var expression = ParseExpression();
        var closeParen = Expect(TokenKind.CloseParen, ")");

        return new UncheckedExpressionNode(uncheckedKeyword, openParen, expression, closeParen);
    }

    private ParserContextScope PushContext(ParserContext parserContext)
    {
        contextStack.Push(parserContext);
        currentContext = parserContext;

        return new ParserContextScope(this);
    }

    private void PopContext()
    {
        if (!contextStack.TryPop(out _) || !contextStack.TryPeek(out currentContext))
            currentContext = ParserContext.None;
    }

    private bool IsTokenValidInPrecedingContext(TokenKind tokenKind)
    {
        foreach (var context in contextStack)
            if (IsTokenValidInContext(context, tokenKind))
                return true;

        return false;
    }

    private bool IsTokenValidInContext(TokenKind tokenKind)
    {
        return IsTokenValidInContext(currentContext, tokenKind);
    }

    private static bool IsTokenValidInContext(ParserContext context, TokenKind tokenKind)
    {
        return context switch
        {
            ParserContext.Namespace => tokenKind.IsValidInNamespace(),
            ParserContext.Type => tokenKind.IsValidInType(),
            ParserContext.ParameterList => tokenKind.IsValidInParameterList(),
            ParserContext.Statement => tokenKind.IsValidInStatement(),
            ParserContext.AttributeList => tokenKind.IsValidInAttributeList(),
            ParserContext.EnumMember => tokenKind.IsValidInEnumMember(),
            ParserContext.Property or ParserContext.Indexer => tokenKind.IsValidInPropertyOrIndexer(),
            ParserContext.Event => tokenKind.IsValidInEvent(),
            _ => false
        };
    }

    private readonly struct ParserContextScope(Parser parser) : IDisposable
    {
        public void Dispose()
        {
            parser.PopContext();
        }
    }
}