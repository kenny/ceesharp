using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record EnumDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken EnumKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenBrace,
    ImmutableArray<EnumMemberDeclarationNode> Members,
    SyntaxToken CloseBrace,
    OptionalSyntax<SyntaxToken> Semicolon) : TypeDeclarationNode, IMemberNode
{
    public static bool IsModifierValid(DeclarationKind declarationContext, TokenKind modifierKind)
    {
        if (declarationContext != DeclarationKind.Namespace && modifierKind == TokenKind.New)
            return true;

        return modifierKind is TokenKind.Public
            or TokenKind.Private
            or TokenKind.Protected
            or TokenKind.Internal;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Members)
            yield return child;
    }

    public override DeclarationKind DeclarationKind => DeclarationKind.Enum;
}