namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record FixedStatementNode(
    SyntaxToken FixedKeyword,
    SyntaxToken OpenParen,
    VariableDeclarationNode Declaration,
    SyntaxToken CloseParen,
    StatementNode Statement) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Declaration;
        yield return Statement;
    }
}