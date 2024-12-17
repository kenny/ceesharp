namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record PostfixUnaryExpressionNode(ExpressionNode Operand, SyntaxToken Operator) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Operand;
    }
}