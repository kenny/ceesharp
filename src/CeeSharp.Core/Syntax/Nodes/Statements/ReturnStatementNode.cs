using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record ReturnStatementNode(
    SyntaxToken ReturnKeyword,
    OptionalSyntax<ExpressionNode> Expression,
    SyntaxToken Semicolon) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        if (Expression.HasValue)
            yield return Expression.Element;
    }
}