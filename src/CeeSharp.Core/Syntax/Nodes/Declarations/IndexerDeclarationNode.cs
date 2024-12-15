using System.Collections.Immutable;
using CeeSharp.Core.Parsing;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record IndexerDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    TypeSyntax Type,
    OptionalSyntax<ExplicitInterfaceNode> ExplicitInterface,
    SyntaxToken ThisKeyword,
    SyntaxToken OpenBracket,
    SeparatedSyntaxList<ParameterNode> Parameters,
    SyntaxToken CloseBracket,
    SyntaxToken OpenBrace,
    ImmutableArray<AccessorDeclarationNode> Accessors,
    SyntaxToken CloseBrace) : MemberDeclarationNode, IModifierValidator
{
    public override DeclarationKind DeclarationKind => DeclarationKind.Property;

    public static bool IsModifierValid(ParserContext parserContext, TokenKind modifier)
    {
        return modifier is TokenKind.New
            or TokenKind.Public
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Private
            or TokenKind.Virtual
            or TokenKind.Sealed
            or TokenKind.Override
            or TokenKind.Abstract
            or TokenKind.Extern;
    }
}