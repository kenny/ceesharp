using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record GotoCaseStatementNode(
    SyntaxToken GotoKeyword,
    SyntaxToken CaseKeyword,
    ExpressionNode Expression,
    SyntaxToken Semicolon)
    : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;
    }
}