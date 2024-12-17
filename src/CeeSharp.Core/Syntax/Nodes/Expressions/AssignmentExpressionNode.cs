namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record AssignmentExpressionNode(ExpressionNode Left, SyntaxToken Operator, ExpressionNode Right)
    : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Left;
        yield return Right;
    }
}