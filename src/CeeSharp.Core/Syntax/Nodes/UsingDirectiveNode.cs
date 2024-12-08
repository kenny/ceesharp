namespace CeeSharp.Core.Syntax.Nodes;

public record UsingDirectiveNode(SyntaxToken UsingKeyword, SyntaxToken Identifier, SyntaxToken Semicolon) : SyntaxNode;