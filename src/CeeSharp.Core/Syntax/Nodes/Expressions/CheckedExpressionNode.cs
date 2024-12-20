namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record CheckedExpressionNode(
    SyntaxToken CheckedKeyword,
    SyntaxToken OpenParen,
    ExpressionNode Expression,
    SyntaxToken CloseParen) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}