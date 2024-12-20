namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record GotoStatementNode(SyntaxToken GotoKeyword, SyntaxToken Identifier, SyntaxToken Semicolon)
    : StatementNode;