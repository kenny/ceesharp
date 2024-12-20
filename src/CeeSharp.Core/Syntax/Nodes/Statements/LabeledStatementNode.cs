namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record LabeledStatementNode(SyntaxToken Identifier, SyntaxToken Colon, StatementNode Statement)
    : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Statement;
    }
}