namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record ParenthesizedExpressionNode(
    SyntaxToken OpenParen,
    ExpressionNode Expression,
    SyntaxToken CloseParen) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}