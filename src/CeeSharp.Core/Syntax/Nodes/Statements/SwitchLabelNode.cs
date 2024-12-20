using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record SwitchLabelNode(
    SyntaxToken CaseOrDefaultKeyword,
    OptionalSyntax<ExpressionNode> Expression,
    SyntaxToken Colon) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        if (Expression.HasValue)
            yield return Expression.Element;
    }
}