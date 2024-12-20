namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record InvocationExpressionNode(
    ExpressionNode Expression,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ArgumentNode> Arguments,
    SyntaxToken CloseParen)
    : ExpressionNode
{
}