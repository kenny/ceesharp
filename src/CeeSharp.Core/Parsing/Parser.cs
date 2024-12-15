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
        var attributes = ParseAttributes();
        var declarations = ParseNamespaceOrTypeDeclarations(DeclarationKind.Namespace);

        if (!TryExpect(TokenKind.EndOfFile, out var endOfFile))
            SkipUntilEnd();

        isInErrorRecovery = false;
        skippedTokens.Clear();

        return new CompilationUnitNode(usings, attributes, declarations, endOfFile);
    }

    private SyntaxToken ExpectIdentifier(DeclarationKind declarationContext = DeclarationKind.None)
    {
        if (!TryExpect(TokenKind.Identifier, out var token, declarationContext))
            diagnostics.ReportError(token.EndTextPosition, "Identifier expected");

        return token;
    }

    private SyntaxToken Expect(TokenKind kind)
    {
        _ = TryExpect(kind, out var token);

        return token;
    }

    private OptionalSyntax<SyntaxToken> ExpectIf(TokenKind kind, bool condition, string text,
        DeclarationKind declarationContext = DeclarationKind.None)
    {
        if (condition)
            return OptionalSyntax.With(Expect(kind, text, declarationContext));

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

    private SyntaxToken Expect(TokenKind kind, string text, DeclarationKind declarationContext = DeclarationKind.None)
    {
        if (!TryExpect(kind, out var token, declarationContext))
            diagnostics.ReportError(Previous.EndPosition, $"{text} expected");

        return token;
    }

    private bool TryExpect(TokenKind kind, out SyntaxToken token,
        DeclarationKind declarationContext = DeclarationKind.None)
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

        if (isInErrorRecovery && declarationContext != DeclarationKind.None)
            Synchronize(declarationContext);

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
        var attributes = ParseAttributes();
        var modifiers = ParseModifiers();

        switch (Current.Kind)
        {
            case TokenKind.Namespace when modifiers.IsEmpty:
                return ParseNamespaceDeclaration();
            case TokenKind.Class:
            case TokenKind.Struct:
            case TokenKind.Enum:
                return ParseTypeDeclaration(declarationContext, attributes, modifiers);
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
            var attributes = ParseAttributes();
            var modifiers = ParseModifiers();
            var declaration = ParseMemberDeclaration(declarationContext, attributes, modifiers);
            if (declaration != null) declarations.Add(declaration);

            if (isInErrorRecovery && IsTokenValidForDeclaration(declarationContext, Current.Kind))
            {
                isInErrorRecovery = false;

                // Do not stop processing if we're in the same context
                if (declarationContext != DeclarationKind.Type)
                    break;
            }

            if (isInErrorRecovery) Synchronize(declarationContext);
        }

        return declarations.ToImmutable();
    }

    private DeclarationNode? ParseMemberDeclaration(DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        switch (Current.Kind)
        {
            case TokenKind.Class:
            case TokenKind.Struct:
            case TokenKind.Enum:
                return ParseTypeDeclaration(declarationContext, attributes, modifiers);

            case TokenKind.Identifier when declarationContext != DeclarationKind.Namespace:
                if (Lookahead.Kind != TokenKind.OpenParen)
                {
                    var type = ParseType();

                    if (Lookahead.Kind is TokenKind.Semicolon or TokenKind.Assign or TokenKind.Comma)
                        return ParseFieldDeclaration(declarationContext, attributes, modifiers, type!);

                    if (!isInErrorRecovery)
                        switch (Lookahead.Kind)
                        {
                            case TokenKind.OpenParen:
                                return ParseMethodDeclaration(declarationContext, attributes, modifiers, type!);
                            case TokenKind.OpenBrace:
                                return ParsePropertyDeclaration(declarationContext, attributes, modifiers, type!);
                        }

                    isInErrorRecovery = false;

                    return HandleIncompleteMember(declarationContext, attributes, modifiers, type!);
                }

                return ParseConstructorDeclaration(declarationContext, attributes, modifiers);
        }

        if (!Current.Kind.IsPredefinedType())
            return HandleIncompleteMember(declarationContext, attributes, modifiers);

        var predefinedType = ParsePredefinedType();

        if (Lookahead.Kind is TokenKind.Semicolon or TokenKind.Assign or TokenKind.Comma)
            return ParseFieldDeclaration(declarationContext, attributes, modifiers, predefinedType!);

        switch (Lookahead.Kind)
        {
            case TokenKind.OpenParen:
                return ParseMethodDeclaration(declarationContext, attributes, modifiers, predefinedType!);
            case TokenKind.OpenBrace:
                return ParsePropertyDeclaration(declarationContext, attributes, modifiers, predefinedType!);
        }

        return null;
    }

    private DeclarationNode HandleIncompleteMember(DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers, params SyntaxElement[] elements)
    {
        if (!isInErrorRecovery)
        {
            diagnostics.ReportError(Current.Position,
                "Invalid member declaration");
            isInErrorRecovery = true;
        }

        Synchronize(declarationContext);

        return new IncompleteMemberDeclarationNode(attributes, modifiers.As<SyntaxElement>().AddRange(elements));
    }

    private DeclarationNode? ParseTypeDeclaration(DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        switch (Current.Kind)
        {
            case TokenKind.Class:
                return ParseClassDeclaration(declarationContext, attributes, modifiers);
            case TokenKind.Struct:
                return ParseStructDeclaration(declarationContext, attributes, modifiers);
            case TokenKind.Enum:
                return ParseEnumDeclaration(declarationContext, attributes, modifiers);
            default:
                if (!isInErrorRecovery) isInErrorRecovery = true;

                return null;
        }
    }

    private EnumDeclarationNode ParseEnumDeclaration(DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<EnumDeclarationNode>(declarationContext, modifiers);

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

            if (isInErrorRecovery && IsTokenValidForDeclaration(DeclarationKind.Enum, Current.Kind))
            {
                isInErrorRecovery = false;
                break;
            }

            if (isInErrorRecovery) Synchronize(DeclarationKind.Enum);
        }

        return members.ToImmutable();
    }

    private EnumMemberDeclarationNode ParseEnumMemberDeclaration(ImmutableArray<AttributeSectionNode> attributes)
    {
        var identifier = ExpectIdentifier(DeclarationKind.EnumMember);
        var assign = ExpectOptional(TokenKind.Assign);
        var expression = assign.HasValue switch
        {
            true => OptionalSyntax.With<ExpressionNode>(new IdentifierExpressionNode(ExpectIdentifier())),
            false => OptionalSyntax<ExpressionNode>.None
        };
        var comma = ExpectIf(TokenKind.Comma, Lookahead.Kind != TokenKind.CloseBrace, ",", DeclarationKind.EnumMember);

        return new EnumMemberDeclarationNode(attributes, identifier, assign, expression, comma);
    }

    private ClassDeclarationNode ParseClassDeclaration(DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ClassDeclarationNode>(declarationContext, modifiers);

        var classKeyword = Expect(TokenKind.Class, "class");
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations(DeclarationKind.Type);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new ClassDeclarationNode(attributes, modifiers, classKeyword, identifier, openBrace, declarations,
            closeBrace);
    }

    private StructDeclarationNode ParseStructDeclaration(DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        ValidateModifiers<ClassDeclarationNode>(declarationContext, modifiers);

        var structKeyword = Expect(TokenKind.Struct, "struct");
        var identifier = ExpectIdentifier();
        var openBrace = Expect(TokenKind.OpenBrace, "{");
        var declarations = ParseTypeDeclarations(DeclarationKind.Struct);
        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new StructDeclarationNode(attributes, modifiers, structKeyword, identifier, openBrace, declarations,
            closeBrace);
    }

    private MethodDeclarationNode ParseMethodDeclaration(DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
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
            attributes,
            modifiers,
            returnType,
            identifier,
            openParen,
            parameters,
            closeParen,
            blockOrSemicolon);
    }

    private ConstructorDeclarationNode ParseConstructorDeclaration(DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
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
            attributes,
            modifiers,
            identifier,
            openParen,
            parameters,
            closeParen,
            blockOrSemicolon);
    }

    private FieldDeclarationNode ParseFieldDeclaration(
        DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        TypeSyntax type)
    {
        ValidateModifiers<FieldDeclarationNode>(declarationContext, modifiers);

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

    private PropertyDeclarationNode ParsePropertyDeclaration(
        DeclarationKind declarationContext,
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        TypeSyntax type)
    {
        ValidateModifiers<PropertyDeclarationNode>(declarationContext, modifiers);

        ParseExplicitInterfaceName(out var explicitInterface, out var dot, out var identifier);

        var openBrace = Expect(TokenKind.OpenBrace, "{");
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
                    if (IsTokenValidForDeclaration(declarationContext, Current.Kind))
                    {
                        accessors.Add(HandleIncompleteAccessor(accessorAttributes, accessorModifiers));

                        break;
                    }

                    isInErrorRecovery = true;

                    Synchronize(DeclarationKind.Property);

                    continue;
                }

                keyword = OptionalSyntax<SyntaxToken>.None;
            }

            accessors.Add(ParseAccessorDeclaration(accessorAttributes, accessorModifiers, keyword));

            if (isInErrorRecovery)
                Synchronize(DeclarationKind.Property);

            if (Current.Kind != TokenKind.Identifier && IsTokenValidForDeclaration(declarationContext, Current.Kind))
                break;
        }

        var closeBrace = Expect(TokenKind.CloseBrace, "}");

        return new PropertyDeclarationNode(
            attributes,
            modifiers,
            type,
            explicitInterface,
            dot,
            identifier,
            openBrace,
            accessors.ToImmutable(),
            closeBrace);
    }

    private AccessorDeclarationNode HandleIncompleteAccessor(ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers)
    {
        return new AccessorDeclarationNode(attributes, modifiers,
            OptionalSyntax<SyntaxToken>.None,
            OptionalSyntax<BlockNode>.None,
            OptionalSyntax<SyntaxToken>.None);
    }

    private void ParseExplicitInterfaceName(out OptionalSyntax<SyntaxToken> explicitInterface,
        out OptionalSyntax<SyntaxToken> dot,
        out SyntaxToken identifier)
    {
        identifier = ExpectIdentifier(DeclarationKind.Property);

        if (Current.Kind != TokenKind.Dot)
        {
            dot = OptionalSyntax<SyntaxToken>.None;
            explicitInterface = OptionalSyntax<SyntaxToken>.None;

            return;
        }

        explicitInterface = OptionalSyntax.With(identifier);
        dot = ExpectOptional(TokenKind.Dot);

        identifier = ExpectIdentifier();
    }

    private AccessorDeclarationNode ParseAccessorDeclaration(
        ImmutableArray<AttributeSectionNode> attributes,
        ImmutableArray<SyntaxToken> modifiers,
        OptionalSyntax<SyntaxToken> keyword)
    {
        if (keyword.HasValue)
            tokenStream.Advance();

        var body = OptionalSyntax<BlockNode>.None;
        var semicolon = OptionalSyntax<SyntaxToken>.None;

        if (Current.Kind == TokenKind.OpenBrace)
            body = OptionalSyntax.With(ParseMethodBody());
        else
            semicolon = OptionalSyntax.With(Expect(TokenKind.Semicolon, ";"));

        return new AccessorDeclarationNode(attributes, modifiers, keyword, body, semicolon);
    }

    private static bool IsTokenValidForDeclaration(DeclarationKind declarationKind, TokenKind tokenKind)
    {
        return declarationKind switch
        {
            DeclarationKind.Namespace => tokenKind.IsModifier() ||
                                         tokenKind is TokenKind.Namespace or TokenKind.Class or TokenKind.Struct
                                             or TokenKind.Enum
                                             or TokenKind.OpenBracket,
            DeclarationKind.Type => tokenKind.IsModifier() || tokenKind.IsPredefinedType() ||
                                    tokenKind is TokenKind.Class or TokenKind.Struct or TokenKind.Enum
                                        or TokenKind.Identifier or TokenKind.CloseBrace,
            DeclarationKind.ParameterList => tokenKind.IsPredefinedType() || tokenKind.IsParameterModifier() ||
                                             tokenKind is TokenKind.Identifier,
            DeclarationKind.AttributeList => tokenKind is TokenKind.Identifier or TokenKind.Comma
                or TokenKind.CloseBracket
                or TokenKind.CloseParen,
            DeclarationKind.EnumMember => tokenKind is TokenKind.Identifier or TokenKind.CloseBrace
                or TokenKind.CloseBracket
                or TokenKind.CloseParen,
            DeclarationKind.Property => tokenKind is TokenKind.Get or TokenKind.Set or TokenKind.OpenBrace
                or TokenKind.CloseBrace,
            _ => false
        };
    }
}