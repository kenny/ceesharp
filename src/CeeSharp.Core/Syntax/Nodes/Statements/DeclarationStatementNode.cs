namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record DeclarationStatementNode(SyntaxNode Declaration, SyntaxToken Semicolon) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Declaration;
    }
}