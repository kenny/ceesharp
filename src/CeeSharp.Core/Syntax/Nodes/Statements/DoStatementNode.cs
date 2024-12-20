using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record DoStatementNode(
    SyntaxToken DoKeyword,
    StatementNode Statement,
    SyntaxToken WhileKeyword,
    SyntaxToken OpenParen,
    ExpressionNode Condition,
    SyntaxToken CloseParen,
    SyntaxToken Semicolon) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Condition;
        yield return Statement;
    }
}