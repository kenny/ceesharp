using System.Collections.Immutable;
using CeeSharp.Core.Parsing;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record DestructorDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken Tilde,
    SyntaxToken Identifier,
    SyntaxToken OpenParen,
    SyntaxToken CloseParen,
    BlockNodeOrToken BlockOrSemicolon) : MemberDeclarationNode, IModifierValidator
{
    public static bool IsModifierValid(ParserContext parserContext, TokenKind modifier)
    {
        return modifier is TokenKind.Extern;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        if (BlockOrSemicolon.IsLeft)
            yield return BlockOrSemicolon.LeftValue;
    }
}