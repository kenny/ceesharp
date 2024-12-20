using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record LockStatementNode(
    SyntaxToken LockKeyword,
    SyntaxToken OpenParen,
    ExpressionNode Expression,
    SyntaxToken CloseParen,
    StatementNode Statement) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
        yield return Statement;
    }
}