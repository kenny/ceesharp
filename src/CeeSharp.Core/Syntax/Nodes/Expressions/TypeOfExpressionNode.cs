namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record TypeOfExpressionNode(
    SyntaxToken TypeOfKeyword,
    SyntaxToken OpenParen,
    ExpressionNode Expression,
    SyntaxToken CloseParen) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}