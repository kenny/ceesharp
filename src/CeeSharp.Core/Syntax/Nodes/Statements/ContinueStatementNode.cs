namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record ContinueStatementNode(SyntaxToken ContinueKeyword, SyntaxToken Semicolon) : StatementNode;