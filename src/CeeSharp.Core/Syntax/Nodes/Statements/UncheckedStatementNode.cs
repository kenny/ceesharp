namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record UncheckedStatementNode(SyntaxToken UncheckedKeyword, BlockStatementNode Block) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Block;
    }
}