using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record WhileStatementNode(
    SyntaxToken WhileKeyword,
    SyntaxToken OpenParen,
    ExpressionNode Condition,
    SyntaxToken CloseParen,
    StatementNode Statement) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Condition;
        yield return Statement;
    }
}