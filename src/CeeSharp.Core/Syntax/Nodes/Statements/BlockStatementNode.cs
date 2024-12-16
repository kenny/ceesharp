using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record BlockStatementNode(
    SyntaxToken OpenBrace,
    ImmutableArray<StatementNode> Statements,
    SyntaxToken CloseBrace) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Statements)
            yield return child;
    }
}