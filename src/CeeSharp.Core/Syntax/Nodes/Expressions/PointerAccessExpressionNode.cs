using System.Linq.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record PointerMemberAccessExpressionNode(ExpressionNode Expression, SyntaxToken Arrow, SyntaxToken Name)
    : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}