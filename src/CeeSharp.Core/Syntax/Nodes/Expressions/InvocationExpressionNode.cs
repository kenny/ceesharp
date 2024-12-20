namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record InvocationExpressionNode(
    ExpressionNode Expression,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ArgumentNode> Arguments,
    SyntaxToken CloseParen)
    : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;

        foreach (var child in Arguments.Elements)
            yield return child;
    }
}