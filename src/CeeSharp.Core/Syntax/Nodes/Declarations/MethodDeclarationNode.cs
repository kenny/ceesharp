using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record MethodDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    TypeSyntax ReturnType,
    SyntaxToken Identifier,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ParameterNode> Parameters,
    SyntaxToken CloseParen,
    SyntaxElement BlockOrSemicolon) : MemberDeclarationNode, IMemberNode
{
    public override DeclarationKind DeclarationKind => DeclarationKind.Method;

    public static bool IsModifierValid(DeclarationKind declarationContext, TokenKind modifier)
    {
        return modifier is TokenKind.Public
            or TokenKind.Private
            or TokenKind.Protected
            or TokenKind.Static
            or TokenKind.Virtual
            or TokenKind.Override
            or TokenKind.Abstract
            or TokenKind.New
            or TokenKind.Sealed
            or TokenKind.Extern;
    }
}