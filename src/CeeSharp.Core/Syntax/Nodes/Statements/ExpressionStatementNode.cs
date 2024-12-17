using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record ExpressionStatementNode(ExpressionNode Expression, SyntaxToken Semicolon) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}