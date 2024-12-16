namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record ElseClauseNode(SyntaxToken ElseKeyword, StatementNode Statement) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Statement;
    }
}