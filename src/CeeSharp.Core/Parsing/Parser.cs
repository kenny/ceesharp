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

                var predefinedType = ParsePredefinedType();

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

        return new EnumDeclarationNode(attributes, modifiers, enumKeyword, identifier, baseType, openBrace, members, closeBrace,
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

        return new ClassDeclarationNode(attributes, modifiers, classKeyword, identifier, baseTypes, openBrace,
            declarations,
            closeBrace);
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

        return new StructDeclarationNode(attributes, modifiers, structKeyword, identifier, baseTypes, openBrace, declarations,
            closeBrace);
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

        return new InterfaceDeclarationNode(
            attributes,
            modifiers,
            interfaceKeyword,
            identifier,
            baseTypes,
            openBrace,
            declarations,
            closeBrace);
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

    private ConstructorDeclarationNode ParseConstructorDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<MethodDeclarationNode>(modifiers);

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
        var variableDeclarators = ParseVariableDeclarators();
        var semicolon = Expect(TokenKind.Semicolon);

        return new EventFieldDeclarationNode(attributes, modifiers, eventKeyword, type, variableDeclarators, semicolon);
    }

    private FieldDeclarationNode ParseFieldDeclaration(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        TypeSyntax type)
    {
        ValidateModifiers<FieldDeclarationNode>(modifiers);

        var variableDeclarators = ParseVariableDeclarators();

        var semicolon = Expect(TokenKind.Semicolon);

        return new FieldDeclarationNode(
            attributes,
            modifiers,
            type,
            variableDeclarators,
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
        }

        return new SeparatedSyntaxList<VariableDeclaratorNode>(declarators.ToImmutable(), separators.ToImmutable());
    }

    private VariableDeclaratorNode ParseVariableDeclarator()
    {
        var identifier = ExpectIdentifier();

        var assign = ExpectOptional(TokenKind.Assign);

        var initializer = assign.HasValue switch
        {
            true => OptionalSyntax.With(ParseExpression()),
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

        var name = ParseQualifiedName();
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

        return Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(),
            TokenKind.If => ParseIfStatement(),
            _ => ParseExpressionStatement()
        };
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

    private OptionalSyntax<ElseClauseNode> ParseElseClause()
    {
        if (Current.Kind != TokenKind.Else)
            return OptionalSyntax<ElseClauseNode>.None;

        var elseKeyword = Expect(TokenKind.Else, "else");
        var statement = ParseStatement();

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