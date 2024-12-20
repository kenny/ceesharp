namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record FinallyClauseNode(
    SyntaxToken FinallyKeyword,
    BlockStatementNode Block) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Block;
    }
}