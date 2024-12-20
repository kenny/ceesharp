using CeeSharp.Core.Syntax.Nodes.Expressions;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record ForeachStatementNode(
    SyntaxToken ForeachKeyword,
    SyntaxToken OpenParen,
    TypeSyntax Type,
    SyntaxToken Identifier,
    SyntaxToken InKeyword,
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