namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record BreakStatementNode(SyntaxToken BreakKeyword, SyntaxToken Semicolon) : StatementNode;