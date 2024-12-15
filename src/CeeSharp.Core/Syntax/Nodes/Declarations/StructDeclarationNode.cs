using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record StructDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken StructKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenBrace,
    ImmutableArray<DeclarationNode> Declarations,
    SyntaxToken CloseBrace) : TypeDeclarationNode, IMemberNode
{
    public override DeclarationKind DeclarationKind => DeclarationKind.Struct;

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
}