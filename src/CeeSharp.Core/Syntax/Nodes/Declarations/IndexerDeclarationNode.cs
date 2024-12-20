using System.Collections.Immutable;
using CeeSharp.Core.Parsing;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record IndexerDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    TypeSyntax Type,
    OptionalSyntax<ExplicitInterfaceNode> ExplicitInterface,
    SyntaxToken ThisKeyword,
    SyntaxToken OpenBracket,
    SeparatedSyntaxList<ParameterNode> Parameters,
    SyntaxToken CloseBracket,
    SyntaxToken OpenBrace,
    ImmutableArray<AccessorDeclarationNode> Accessors,
    SyntaxToken CloseBrace) : MemberDeclarationNode, IModifierValidator
{
    public static bool IsModifierValid(ParserContext parserContext, TokenKind modifier)
    {
        return modifier is TokenKind.New
            or TokenKind.Public
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Private
            or TokenKind.Virtual
            or TokenKind.Sealed
            or TokenKind.Override
            or TokenKind.Abstract
            or TokenKind.Extern
            or TokenKind.Unsafe;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        if (ExplicitInterface.HasValue)
            yield return ExplicitInterface.Element;

        foreach (var child in Parameters.Elements)
            yield return child;

        foreach (var child in Accessors)
            yield return child;
    }
}