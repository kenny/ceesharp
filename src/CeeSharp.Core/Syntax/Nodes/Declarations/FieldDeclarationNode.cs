using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record FieldDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    TypeSyntax Type,
    SeparatedSyntaxList<VariableDeclaratorNode> Declarators,
    SyntaxToken Semicolon) : MemberDeclarationNode, IMemberNode
{
    
    public static bool IsModifierValid(DeclarationKind declarationContext, TokenKind modifierKind)
    {
        if (declarationContext != DeclarationKind.Namespace && modifierKind == TokenKind.New)
            return true;

        return modifierKind is TokenKind.Public
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Private
            or TokenKind.Static
            or TokenKind.Readonly
            or TokenKind.Volatile;
    }
    
    public override DeclarationKind DeclarationKind => DeclarationKind.Field;
}