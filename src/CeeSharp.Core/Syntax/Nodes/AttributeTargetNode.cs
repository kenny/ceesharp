namespace CeeSharp.Core.Syntax.Nodes;

public sealed record AttributeTargetNode(SyntaxToken Identifier, SyntaxToken Colon) : SyntaxNode;