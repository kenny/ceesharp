using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record AsExpressionNode(ExpressionNode Left, SyntaxToken AsKeyword, TypeSyntax Right) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Left;
    }
}