using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record CastExpressionNode(
    SyntaxToken OpenParen,
    TypeSyntax Type,
    SyntaxToken CloseParen,
    ExpressionNode Expression)
    : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}