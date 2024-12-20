namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record CheckedStatementNode(SyntaxToken CheckedKeyword, BlockStatementNode Block) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Block;
    }
}