namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record ConditionalExpressionNode(
    ExpressionNode Condition,
    SyntaxToken Question,
    ExpressionNode IfTrue,
    SyntaxToken Colon,
    ExpressionNode IfFalse) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Condition;
        yield return IfTrue;
        yield return IfFalse;
    }
}