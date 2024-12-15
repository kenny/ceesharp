using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record ConstructorDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken Identifier,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ParameterNode> Parameters,
    SyntaxToken CloseParen,
    SyntaxElement BlockOrSemicolon) : MemberDeclarationNode, IMemberNode
{
    public override DeclarationKind DeclarationKind => DeclarationKind.Constructor;

    public static bool IsModifierValid(DeclarationKind declarationContext, TokenKind modifier)
    {
        return modifier is TokenKind.Public
            or TokenKind.Private
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Static
            or TokenKind.Extern;
    }
}