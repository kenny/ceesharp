using System.Collections.Immutable;
using CeeSharp.Core.Parsing;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record EnumDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken EnumKeyword,
    SyntaxToken Identifier,
    OptionalSyntax<BaseTypeListNode> BaseType,
    SyntaxToken OpenBrace,
    ImmutableArray<EnumMemberDeclarationNode> Members,
    SyntaxToken CloseBrace,
    OptionalSyntax<SyntaxToken> Semicolon) : TypeDeclarationNode, IModifierValidator
{
    public static bool IsModifierValid(ParserContext parserContext, TokenKind modifier)
    {
        if (parserContext != ParserContext.Namespace && modifier == TokenKind.New)
            return true;

        return modifier is TokenKind.Public
            or TokenKind.Private
            or TokenKind.Protected
            or TokenKind.Internal;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        if (BaseType.HasValue)
            yield return BaseType.Element;

        foreach (var child in Members)
            yield return child;
    }
}