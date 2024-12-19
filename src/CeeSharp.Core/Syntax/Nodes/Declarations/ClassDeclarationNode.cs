using System.Collections.Immutable;
using CeeSharp.Core.Parsing;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record ClassDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken ClassKeyword,
    SyntaxToken Identifier,
    OptionalSyntax<BaseTypeListNode> BaseTypes,
    SyntaxToken OpenBrace,
    ImmutableArray<DeclarationNode> Declarations,
    SyntaxToken CloseBrace) : TypeDeclarationNode, IModifierValidator
{
    public static bool IsModifierValid(ParserContext parserContext, TokenKind modifier)
    {
        if (parserContext != ParserContext.Namespace && modifier == TokenKind.New)
            return true;

        return modifier is TokenKind.Public
            or TokenKind.Private
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Abstract
            or TokenKind.Sealed
            or TokenKind.Unsafe;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        if (BaseTypes.HasValue)
            yield return BaseTypes.Element;
        
        foreach (var child in Declarations)
            yield return child;
    }
}