using System.Collections.Immutable;
using CeeSharp.Core.Parsing;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record OperatorDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    TypeSyntax ReturnType,
    SyntaxToken OperatorKeyword,
    SyntaxToken Operator,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ParameterNode> Parameters,
    SyntaxToken CloseParen,
    BlockNodeOrToken BlockOrSemicolon) : MemberDeclarationNode, IModifierValidator
{ 
    public static bool IsModifierValid(ParserContext parserContext, TokenKind modifier)
    {
        return modifier is TokenKind.Public
            or TokenKind.Static
            or TokenKind.Extern;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        foreach (var child in Parameters.Elements)
            yield return child;

        if (BlockOrSemicolon.IsLeft)
            yield return BlockOrSemicolon.LeftValue;
    }
}