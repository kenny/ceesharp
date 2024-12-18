using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record ArgumentNode(OptionalSyntax<SyntaxToken> RefOrOut, ExpressionNode Expression) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}