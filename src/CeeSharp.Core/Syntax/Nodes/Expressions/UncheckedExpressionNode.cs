namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record UncheckedExpressionNode(
    SyntaxToken UncheckedKeyword,
    SyntaxToken OpenParen,
    ExpressionNode Expression,
    SyntaxToken CloseParen) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}