using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record ConstructorDeclarationNode(
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken Identifier,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ParameterNode> Parameters,
    SyntaxToken CloseParen,
    SyntaxToken OpenBrace,
    SyntaxToken CloseBrace) : MemberDeclarationNode, IMemberNode
{
    public static bool IsModifierValid(DeclarationKind declarationContext, TokenKind modifier)
    {
        return modifier is TokenKind.Public 
            or TokenKind.Private 
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Static 
            or TokenKind.Extern;
    }

    public override DeclarationKind DeclarationKind => DeclarationKind.Constructor;
}