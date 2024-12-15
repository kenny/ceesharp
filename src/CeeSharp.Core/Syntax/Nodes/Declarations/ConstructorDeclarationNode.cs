using System.Collections.Immutable;
using CeeSharp.Core.Parsing;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record ConstructorDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken Identifier,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ParameterNode> Parameters,
    SyntaxToken CloseParen,
    SyntaxElement BlockOrSemicolon) : MemberDeclarationNode, IModifierValidator
{
    public override DeclarationKind DeclarationKind => DeclarationKind.Constructor;

    public static bool IsModifierValid(ParserContext parserContext, TokenKind modifier)
    {
        return modifier is TokenKind.Public
            or TokenKind.Private
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Static
            or TokenKind.Extern;
    }
}