using System.Collections.Immutable;
using CeeSharp.Core.Parsing;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record EventDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken EventKeyword,
    TypeSyntax Type,
    OptionalSyntax<ExplicitInterfaceNode> ExplicitInterface,
    SyntaxToken Identifier,
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
            or TokenKind.Static
            or TokenKind.Virtual
            or TokenKind.Sealed
            or TokenKind.Override
            or TokenKind.Abstract
            or TokenKind.Extern;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        foreach (var child in Accessors)
            yield return child;
    }
}