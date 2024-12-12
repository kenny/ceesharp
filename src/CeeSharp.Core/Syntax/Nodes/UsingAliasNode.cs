namespace CeeSharp.Core.Syntax.Nodes;

public record UsingAliasNode(SyntaxToken Identifier, SyntaxToken Assign) : SyntaxNode;