using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record ArrayRankSpecifierNode(
    SyntaxToken OpenBracket,
    SeparatedSyntaxList<ExpressionNode> Sizes,
    SyntaxToken CloseBracket) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Sizes.Elements)
            yield return child;
    }
}