using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record IfStatementNode(
    SyntaxToken IfKeyword,
    SyntaxToken OpenParen,
    ExpressionNode Condition,
    SyntaxToken CloseParen,
    StatementNode Statement,
    OptionalSyntax<ElseClauseNode> ElseClause) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Statement;

        if (ElseClause.HasValue)
            yield return ElseClause.Element;
    }
}