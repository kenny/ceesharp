using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record ClassDeclarationNode(
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken ClassKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenBrace,
    ImmutableArray<DeclarationNode> Declarations,
    SyntaxToken CloseBrace) : DeclarationNode, IMemberNode
{
    public static bool IsModifierValid(DeclarationKind declarationContext, TokenKind modifierKind)
    {
        if (declarationContext != DeclarationKind.Namespace && modifierKind == TokenKind.New)
            return true;
        
        return modifierKind is TokenKind.Public
            or TokenKind.Private
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Abstract
            or TokenKind.Sealed
            or TokenKind.Unsafe;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Declarations)
            yield return child;
    }

    public override DeclarationKind DeclarationKind => DeclarationKind.Class;
}