namespace CeeSharp.Core.Syntax.Nodes;

public sealed record UsingAliasNode(SyntaxToken Identifier, SyntaxToken Assign) : SyntaxNode;