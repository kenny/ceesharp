namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record PrefixUnaryExpressionNode(SyntaxToken Operator, ExpressionNode Operand) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Operand;
    }
}