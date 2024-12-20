namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record UnsafeStatementNode(SyntaxToken UnsafeKeyword, BlockStatementNode Block) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Block;
    }
}