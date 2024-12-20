namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record MemberAccessExpressionNode(ExpressionNode Expression, SyntaxToken Dot, SyntaxToken Name)
    : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}