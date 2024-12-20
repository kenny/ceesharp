namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record GotoDefaultStatementNode(
    SyntaxToken GotoKeyword,
    SyntaxToken DefaultKeyword,
    SyntaxToken Semicolon)
    : StatementNode;